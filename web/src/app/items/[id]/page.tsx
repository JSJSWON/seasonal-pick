import Link from "next/link";
import { ApiUnavailable, Shell } from "@/components/Shell";
import { RegionSelect } from "@/components/RegionSelect";
import { PriceSummary } from "@/components/PriceSummary";
import { TrendChart } from "@/components/TrendChart";
import { getPrices, getRegions, getTargets } from "@/lib/api";

export const instant = false;

/**
 * One item's prices: current price, verdict, and the monthly trend.
 *
 * The region stays in the query string so the page is a server component
 * with nothing to keep in sync, and a link to it reopens the same view.
 */
export default async function ItemDetail({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ region?: string }>;
}) {
  const { id } = await params;
  const { region } = await searchParams;
  const targetId = Number(id);

  if (!Number.isInteger(targetId)) {
    return <NotFound />;
  }

  let targets;
  let regions;
  try {
    targets = await getTargets();
    regions = await getRegions(targetId);
  } catch {
    return <ApiUnavailable />;
  }

  const target = targets.find((t) => t.id === targetId);
  if (!target) {
    return <NotFound />;
  }

  const selectedRegion =
    regions.find((r) => r.code === region) ??
    regions.find((r) => r.code === "1101") ??
    regions[0];

  let detail = null;
  if (selectedRegion) {
    try {
      detail = await getPrices(targetId, selectedRegion.code);
    } catch {
      return <ApiUnavailable />;
    }
  }

  return (
    <Shell>
      <div className="mb-6">
        <p className="text-sm text-neutral-500">{target.categoryName}</p>
        <h2 className="text-xl font-semibold text-neutral-900">
          {target.displayName}
        </h2>
      </div>

      {selectedRegion ? (
        <RegionSelect
          basePath={`/items/${targetId}`}
          options={regions}
          selectedCode={selectedRegion.code}
        />
      ) : (
        <p className="text-neutral-600">이 품목은 조사된 지역이 없습니다.</p>
      )}

      {detail && (
        <div className="mt-8 space-y-8">
          <PriceSummary
            latest={detail.latest}
            verdict={detail.verdict}
            staleDays={detail.staleDays}
          />
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

/** Shown for an id that is not an active target. */
function NotFound() {
  return (
    <Shell>
      <p className="text-neutral-600">
        그 품목을 찾을 수 없습니다.{" "}
        <Link href="/" className="text-emerald-700 underline">
          추천 목록으로
        </Link>
      </p>
    </Shell>
  );
}
