import type { MonthlyAverage } from "@/lib/api";
import { formatMonth, formatPrice } from "@/lib/format";

/** Months since January 1970, so points can be placed on a real time axis. */
function monthIndex(isoDate: string): number {
  const [year, month] = isoDate.split("-").map(Number);
  return year * 12 + (month - 1);
}

/**
 * Splits the series wherever a month is missing.
 *
 * Drawn as one unbroken line, a seasonal variety would look continuous: 가을
 * 배추 exists only from October to January, so joining January to the next
 * October draws a line across eight months that were never surveyed. That is
 * the same kind of invented trend the market-level API was rejected for.
 * Each run of consecutive months becomes its own polyline instead.
 */
function splitIntoRuns(monthly: MonthlyAverage[]): MonthlyAverage[][] {
  const runs: MonthlyAverage[][] = [];
  let run: MonthlyAverage[] = [];

  for (const point of monthly) {
    const previous = run.at(-1);
    if (previous && monthIndex(point.month) !== monthIndex(previous.month) + 1) {
      runs.push(run);
      run = [];
    }
    run.push(point);
  }

  if (run.length > 0) {
    runs.push(run);
  }

  return runs;
}

/**
 * Monthly average prices over time, with the most recent month marked.
 *
 * Inline SVG rather than a charting library: one line and one point is little
 * enough that a dependency would cost more than it saves, and what the chart
 * should eventually show is not settled. Axes, tooltips or zoom would be the
 * moment to pick one.
 */
export function TrendChart({ monthly }: { monthly: MonthlyAverage[] }) {
  if (monthly.length < 2) {
    return (
      <p className="text-sm text-neutral-500">
        추세를 그릴 만큼의 월별 데이터가 없습니다.
      </p>
    );
  }

  const width = 720;
  const height = 240;
  const padding = { top: 16, right: 16, bottom: 28, left: 64 };
  const plotWidth = width - padding.left - padding.right;
  const plotHeight = height - padding.top - padding.bottom;

  const values = monthly.map((m) => m.average);
  const lowest = Math.min(...values);
  const highest = Math.max(...values);
  // A flat series would divide by zero; give it a band so the line lands in
  // the middle rather than disappearing.
  const span = highest - lowest || Math.max(highest, 1);

  const firstMonth = monthIndex(monthly[0].month);
  const lastMonth = monthIndex(monthly[monthly.length - 1].month);
  const monthSpan = lastMonth - firstMonth || 1;

  const xOf = (isoMonth: string) =>
    padding.left + (plotWidth * (monthIndex(isoMonth) - firstMonth)) / monthSpan;
  const yOf = (value: number) =>
    padding.top + plotHeight - (plotHeight * (value - lowest)) / span;

  const runs = splitIntoRuns(monthly);
  const last = monthly[monthly.length - 1];

  // Ticks come from the time range, not from the data, so a year with no
  // January still gets its label in the right place.
  const firstYear = Number(monthly[0].month.slice(0, 4));
  const lastYear = Number(last.month.slice(0, 4));
  const yearTicks = Array.from(
    { length: lastYear - firstYear + 1 },
    (_, offset) => firstYear + offset,
  ).filter((year) => {
    const january = year * 12;
    return january >= firstMonth && january <= lastMonth;
  });

  return (
    <figure className="w-full">
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="h-auto w-full"
        role="img"
        aria-label={`${formatMonth(monthly[0].month)}부터 ${formatMonth(
          last.month,
        )}까지의 월평균 가격 추이`}
      >
        <line
          x1={padding.left}
          y1={padding.top + plotHeight}
          x2={width - padding.right}
          y2={padding.top + plotHeight}
          className="stroke-neutral-300"
          strokeWidth={1}
        />

        {[highest, lowest].map((value) => (
          <g key={value}>
            <line
              x1={padding.left}
              y1={yOf(value)}
              x2={width - padding.right}
              y2={yOf(value)}
              className="stroke-neutral-200"
              strokeDasharray="3 3"
              strokeWidth={1}
            />
            <text
              x={padding.left - 8}
              y={yOf(value) + 4}
              textAnchor="end"
              className="fill-neutral-500 text-[11px]"
            >
              {formatPrice(Math.round(value))}
            </text>
          </g>
        ))}

        {yearTicks.map((year) => (
          <text
            key={year}
            x={padding.left + (plotWidth * (year * 12 - firstMonth)) / monthSpan}
            y={height - 8}
            textAnchor="middle"
            className="fill-neutral-500 text-[11px]"
          >
            {year}
          </text>
        ))}

        {runs.map((run) =>
          run.length === 1 ? (
            // A single surveyed month has no line to draw, so it is shown as
            // a dot rather than dropped.
            <circle
              key={run[0].month}
              cx={xOf(run[0].month)}
              cy={yOf(run[0].average)}
              r={2}
              className="fill-emerald-600"
            />
          ) : (
            <polyline
              key={run[0].month}
              points={run.map((m) => `${xOf(m.month)},${yOf(m.average)}`).join(" ")}
              fill="none"
              className="stroke-emerald-600"
              strokeWidth={2}
              strokeLinejoin="round"
            />
          ),
        )}

        <circle
          cx={xOf(last.month)}
          cy={yOf(last.average)}
          r={4}
          className="fill-emerald-700"
        />
      </svg>
      <figcaption className="mt-2 text-sm text-neutral-500">
        {formatMonth(monthly[0].month)} ~ {formatMonth(last.month)} 월평균 · 총{" "}
        {monthly.length}개월
        {runs.length > 1 && ` · 조사가 없던 기간은 선을 끊었습니다`}
      </figcaption>
    </figure>
  );
}
