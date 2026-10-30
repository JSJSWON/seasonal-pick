import { Picker, type PickerOption } from "@/components/Picker";
import { PriceSummary } from "@/components/PriceSummary";
import { TrendChart } from "@/components/TrendChart";
import { getPrices, getRegions, getTargets } from "@/lib/api";
import { formatSurveyDate } from "@/lib/format";

/**
 * Renders on every request rather than being prerendered.
 *
 * The page is nothing but data: the item list, the region list and the prices
 * all come from the API, and caching them would mostly serve a stale survey
 * date, which is the one thing this screen must be honest about. Streaming
 * the static header first with `<Suspense>` would read better and is worth
 * doing once the layout settles.
 */
export const instant = false;

/**
 * The one screen: pick an item, pick one of its regions, read its prices.
 *
 * A server component with the whole selection in the query string. The item
 * comes before the region because which regions survey which item is aT's
 * business and it changes - choosing a region first would let a reader land
 * on a city where the item they wanted has never been surveyed.
 */
export default async function Home({
  searchParams,
}: {
  searchParams: Promise<{ target?: string; region?: string }>;
}) {
  const params = await searchParams;

  let targets;
  try {
    targets = await getTargets();
  } catch {
    return <ApiUnavailable />;
  }

  if (targets.length === 0) {
    return (
      <Shell>
        <p className="text-neutral-600">수집 대상이 없습니다.</p>
      </Shell>
    );
  }

  const selectedTarget =
    targets.find((t) => String(t.id) === params.target) ?? targets[0];

  let regions;
  try {
    regions = await getRegions(selectedTarget.id);
  } catch {
    return <ApiUnavailable />;
  }

  const selectedRegion =
    regions.find((r) => r.code === params.region) ??
    regions.find((r) => r.code === "1101") ??
    regions[0];

  const targetOptions: PickerOption[] = targets.map((target) => ({
    key: String(target.id),
    // Dropping the region on an item change is deliberate: the next item may
    // not be surveyed where this one is, and the default picks itself.
    href: `/?target=${target.id}`,
    label: target.displayName,
    note: target.categoryName,
    selected: target.id === selectedTarget.id,
  }));

  const regionOptions: PickerOption[] = regions.map((region) => ({
    key: region.code,
    href: `/?target=${selectedTarget.id}&region=${region.code}`,
    label: region.name,
    note: region.latestDate ? `${formatSurveyDate(region.latestDate)}까지` : undefined,
    selected: region.code === selectedRegion?.code,
  }));

  let detail = null;
  if (selectedRegion) {
    try {
      detail = await getPrices(selectedTarget.id, selectedRegion.code);
    } catch {
      return <ApiUnavailable />;
    }
  }

  return (
    <Shell>
      <div className="space-y-6">
        <Picker legend="품목" options={targetOptions} />
        {regionOptions.length > 0 ? (
          <Picker legend="지역" options={regionOptions} />
        ) : (
          <p className="text-neutral-600">
            이 품목은 조사된 지역이 없습니다.
          </p>
        )}
      </div>

      {detail && (
        <div className="mt-8 space-y-8">
          <PriceSummary latest={detail.latest} verdict={detail.verdict} />
          <section>
            <h2 className="mb-3 text-sm font-medium text-neutral-500">
              월평균 추이
            </h2>
            <TrendChart monthly={detail.monthly} />
          </section>
        </div>
      )}
    </Shell>
  );
}

/** Page frame: title, the two-question subtitle, and the content. */
function Shell({ children }: { children: React.ReactNode }) {
  return (
    <main className="mx-auto max-w-3xl px-4 py-10">
      <header className="mb-8">
        <h1 className="text-2xl font-semibold tracking-tight text-neutral-900">
          seasonal-pick
        </h1>
        <p className="mt-1 text-neutral-600">
          이 가격이 평소보다 싼지 알려줍니다.
        </p>
      </header>
      {children}
    </main>
  );
}

/**
 * Shown when the API cannot be reached.
 *
 * Named rather than inlined because all three calls fail the same way, and a
 * reader seeing a blank page would not know the collector or the API is down
 * rather than the data being missing.
 */
function ApiUnavailable() {
  return (
    <Shell>
      <div className="rounded-xl border border-amber-300 bg-amber-50 p-5">
        <p className="font-medium text-amber-900">조회 API에 연결할 수 없습니다.</p>
        <p className="mt-1 text-sm text-amber-800">
          <code className="rounded bg-amber-100 px-1">
            cd api/SeasonalPick.Api &amp;&amp; dotnet run
          </code>{" "}
          으로 띄운 뒤 새로고침하세요. 다른 주소를 쓰면{" "}
          <code className="rounded bg-amber-100 px-1">API_BASE_URL</code>을
          설정하세요.
        </p>
      </div>
    </Shell>
  );
}
