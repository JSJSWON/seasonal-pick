"use client";

import { useState } from "react";
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
 * Inline SVG rather than a charting library: one line, one point and a hover
 * readout is little enough that a dependency would cost more than it saves.
 * Zoom or several series would be the moment to pick one.
 *
 * A client component only for the hover readout. The pointer snaps to the
 * nearest month that has data, so a gap in a seasonal item's series never
 * shows a value that was not surveyed.
 */
export function TrendChart({ monthly }: { monthly: MonthlyAverage[] }) {
  const [hovered, setHovered] = useState<MonthlyAverage | null>(null);

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

  /** Picks the month nearest the pointer, converting screen pixels to SVG units. */
  const handlePointerMove = (event: React.PointerEvent<SVGSVGElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    const x = ((event.clientX - rect.left) * width) / rect.width;

    let nearest = monthly[0];
    for (const point of monthly) {
      if (Math.abs(xOf(point.month) - x) < Math.abs(xOf(nearest.month) - x)) {
        nearest = point;
      }
    }
    setHovered(nearest);
  };

  const readout = hovered
    ? `${formatMonth(hovered.month)} · ${formatPrice(Math.round(hovered.average))}`
    : "";
  // Roughly 7px per character at this font size; the label flips to the left
  // of the point when it would run past the plot's right edge.
  const readoutWidth = readout.length * 7 + 16;
  const readoutOnLeft =
    hovered !== null && xOf(hovered.month) + 12 + readoutWidth > width - padding.right;

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
        className="h-auto w-full touch-pan-y"
        onPointerMove={handlePointerMove}
        onPointerLeave={() => setHovered(null)}
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

        {hovered && (
          <g pointerEvents="none">
            <line
              x1={xOf(hovered.month)}
              y1={padding.top}
              x2={xOf(hovered.month)}
              y2={padding.top + plotHeight}
              className="stroke-neutral-400"
              strokeDasharray="3 3"
              strokeWidth={1}
            />
            <circle
              cx={xOf(hovered.month)}
              cy={yOf(hovered.average)}
              r={5}
              className="fill-emerald-600 stroke-white"
              strokeWidth={2}
            />
            <rect
              x={
                readoutOnLeft
                  ? xOf(hovered.month) - 12 - readoutWidth
                  : xOf(hovered.month) + 12
              }
              y={Math.max(2, yOf(hovered.average) - 32)}
              width={readoutWidth}
              height={24}
              rx={6}
              className="fill-white stroke-neutral-300"
            />
            <text
              x={
                readoutOnLeft
                  ? xOf(hovered.month) - 12 - readoutWidth + 8
                  : xOf(hovered.month) + 12 + 8
              }
              y={Math.max(2, yOf(hovered.average) - 32) + 16}
              className="fill-neutral-900 text-[12px]"
            >
              {readout}
            </text>
          </g>
        )}
      </svg>
      <figcaption className="mt-2 text-sm text-neutral-500">
        {formatMonth(monthly[0].month)} ~ {formatMonth(last.month)} 월평균 · 총{" "}
        {monthly.length}개월
        {runs.length > 1 && ` · 조사가 없던 기간은 선을 끊었습니다`}
      </figcaption>
    </figure>
  );
}
