/** Formatting shared by the screen's components. */

/** Formats a price with thousands separators, e.g. 62900 -> "62,900원". */
export function formatPrice(won: number): string {
  return `${won.toLocaleString("ko-KR")}원`;
}

/**
 * Formats a unit the way a shelf label would read.
 *
 * A unit size of 1 is left out, because "1포기당" reads worse than "포기당".
 *
 * @param unit The unit aT quotes, e.g. "kg" or "포기".
 * @param unitSize How many of them, e.g. 20 for a 20kg bag.
 */
export function formatUnit(unit: string, unitSize: number): string {
  return unitSize === 1 ? unit : `${unitSize}${unit}`;
}

/** Formats an ISO date as "10월 29일". */
export function formatSurveyDate(isoDate: string): string {
  const [, month, day] = isoDate.split("-");
  return `${Number(month)}월 ${Number(day)}일`;
}

/** Formats a month key as "2026.10". */
export function formatMonth(isoDate: string): string {
  const [year, month] = isoDate.split("-");
  return `${year}.${month}`;
}
