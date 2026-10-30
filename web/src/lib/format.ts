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

/**
 * Formats a survey date, keeping the year when it is not the current one.
 *
 * The date is shown so a reader can judge how stale a region's data is, and
 * dropping the year defeated that: 김해's last survey for summer cabbage is
 * 2025-11-05 and read as "11월 5일" - a week away rather than a year old.
 * Anything from an earlier year now says so.
 */
export function formatSurveyDate(isoDate: string): string {
  const [year, month, day] = isoDate.split("-");
  const thisYear = String(new Date().getFullYear());

  return year === thisYear
    ? `${Number(month)}월 ${Number(day)}일`
    : `${year}년 ${Number(month)}월 ${Number(day)}일`;
}

/** Formats a month key as "2026.10". */
export function formatMonth(isoDate: string): string {
  const [year, month] = isoDate.split("-");
  return `${year}.${month}`;
}
