import { ApiUnavailable, Shell } from "@/components/Shell";
import { Picker, type PickerOption } from "@/components/Picker";
import { RecommendationCard } from "@/components/RecommendationCard";
import { getAllRegions, getRecommendations, getTargets } from "@/lib/api";
import { formatSurveyDate } from "@/lib/format";

export const instant = false;

/**
 * The home screen: what is unusually cheap in one region right now.
 *
 * Region comes first here, unlike the detail page where the item does. The
 * reason is the same in both cases - never offer a choice that leads
 * nowhere. On the detail page the item is known, so its regions can be
 * listed; here no item is known yet, so the region narrows first and the
 * list is built from whatever that region actually holds.
 *
 * Season is deliberately absent from the ranking. It is reference material,
 * not evidence strong enough to decide what to recommend (설계 1.2).
 */
export default async function Home({
  searchParams,
}: {
  searchParams: Promise<{ region?: string }>;
}) {
  const { region } = await searchParams;

  let regions;
  try {
    regions = await getAllRegions();
  } catch {
    return <ApiUnavailable />;
  }

  if (regions.length === 0) {
    return (
      <Shell>
        <p className="text-neutral-600">아직 수집된 가격이 없습니다.</p>
      </Shell>
    );
  }

  const selectedRegion =
    regions.find((r) => r.code === region) ??
    regions.find((r) => r.code === "1101") ??
    regions[0];

  const regionOptions: PickerOption[] = regions.map((r) => ({
    key: r.code,
    href: `/?region=${r.code}`,
    label: r.name,
    note: r.latestDate ? `${formatSurveyDate(r.latestDate)}까지` : undefined,
    selected: r.code === selectedRegion.code,
  }));

  let list;
  let targets;
  try {
    list = await getRecommendations(selectedRegion.code);
    targets = await getTargets();
  } catch {
    return <ApiUnavailable />;
  }

  // Every item, so the screen still leads somewhere on a day when nothing is
  // cheap. An empty recommendation list is a legitimate answer, and without
  // this it would look like a broken service.
  const allItems: PickerOption[] = targets.map((t) => ({
    key: String(t.id),
    href: `/items/${t.id}?region=${selectedRegion.code}`,
    label: t.displayName,
    note: t.categoryName,
    selected: false,
  }));

  return (
    <Shell>
      <Picker legend="지역" options={regionOptions} />

      <section className="mt-8">
        <h2 className="mb-1 text-lg font-semibold text-neutral-900">
          요즘 평년보다 싼 품목
        </h2>
        <p className="mb-4 text-sm text-neutral-500">
          {list.asOf
            ? `${formatSurveyDate(list.asOf)} 조사 기준`
            : "조사 기록이 없습니다"}
        </p>

        {list.items.length > 0 ? (
          <ul className="grid gap-3 sm:grid-cols-2">
            {list.items.map((item) => (
              <RecommendationCard
                key={item.target.id}
                recommendation={item}
                regionCode={selectedRegion.code}
              />
            ))}
          </ul>
        ) : (
          <p className="rounded-xl border border-neutral-200 bg-white p-5 text-neutral-600">
            이 지역에는 지금 평년보다 뚜렷이 싼 품목이 없습니다. 품목별 가격은
            아래에서 볼 수 있습니다.
          </p>
        )}
      </section>

      <section className="mt-10">
        <Picker legend="전체 품목" options={allItems} />
      </section>
    </Shell>
  );
}
