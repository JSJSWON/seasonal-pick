import type { LatestPrice, Verdict } from "@/lib/api";
import { formatPrice, formatSurveyDate, formatUnit } from "@/lib/format";

/**
 * The current price and what it means, side by side.
 *
 * The survey date is shown next to the price rather than implied, because it
 * is routinely not today: aT does not survey weekends or holidays and
 * registers some dates days late. Showing "10월 29일 기준" is honest where
 * showing a bare price would not be.
 */
export function PriceSummary({
  latest,
  verdict,
}: {
  latest?: LatestPrice;
  verdict?: Verdict;
}) {
  if (!latest || latest.avg === undefined) {
    return (
      <p className="rounded-xl border border-neutral-200 bg-white p-5 text-neutral-600">
        이 지역에는 조사된 가격이 없습니다.
      </p>
    );
  }

  const hasRange =
    latest.min !== undefined &&
    latest.max !== undefined &&
    latest.min !== latest.max;

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <div className="rounded-xl border border-neutral-200 bg-white p-5">
        <p className="text-sm text-neutral-500">
          {formatSurveyDate(latest.date)} 기준
        </p>
        <p className="mt-1 text-3xl font-semibold tracking-tight text-neutral-900">
          {formatPrice(latest.avg)}
          <span className="ml-1 text-base font-normal text-neutral-500">
            / {formatUnit(latest.unit, latest.unitSize)}
          </span>
        </p>
        {hasRange && (
          <p className="mt-2 text-sm text-neutral-600">
            최저 {formatPrice(latest.min!)} · 최고 {formatPrice(latest.max!)}
          </p>
        )}
      </div>

      <div className="rounded-xl border border-neutral-200 bg-white p-5">
        <p className="text-sm text-neutral-500">평년 같은 달과 비교</p>
        {verdict?.available ? (
          <>
            <p className="mt-1 text-2xl font-semibold text-neutral-900">
              {verdict.label}
            </p>
            <p className="mt-2 text-sm text-neutral-600">
              평년값 {formatPrice(verdict.baseline!)}
              {verdict.differencePercent !== undefined &&
                ` · 평년 대비 ${verdict.differencePercent}%`}
            </p>
            <p className="mt-1 text-xs text-neutral-500">
              과거 같은 달 {verdict.sampleSize}년 · z {verdict.z}
            </p>
          </>
        ) : (
          <p className="mt-1 text-neutral-600">
            {verdict?.label ?? "비교할 과거 데이터가 없습니다."}
          </p>
        )}
      </div>
    </div>
  );
}
