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
 * Formats a survey date with its year, e.g. "2026년 10월 23일".
 *
 * The date is shown so a reader can judge how stale the data is. The year is
 * always included: dropping it for the current year read 김해's last summer
 * cabbage survey (2025-11-05) as "11월 5일" - a week away rather than a year
 * old - and a date that sometimes has a year is harder to scan than one that
 * always does.
 */
export function formatSurveyDate(isoDate: string): string {
  const [year, month, day] = isoDate.split("-");
  return `${year}년 ${Number(month)}월 ${Number(day)}일`;
}

/**
 * Formats the moment the collector last ran as a Korean calendar date.
 *
 * Read in Asia/Seoul rather than the server's zone, because the timestamp is
 * a UTC instant and the reader's "today" is Korean.
 */
export function formatCollectedDate(isoTimestamp: string): string {
  const parts = new Intl.DateTimeFormat("ko-KR", {
    timeZone: "Asia/Seoul",
    year: "numeric",
    month: "numeric",
    day: "numeric",
  }).formatToParts(new Date(isoTimestamp));
  const part = (type: string) => parts.find((p) => p.type === type)!.value;

  return `${part("year")}년 ${part("month")}월 ${part("day")}일`;
}

/** Formats a month key as "2026.10". */
export function formatMonth(isoDate: string): string {
  const [year, month] = isoDate.split("-");
  return `${year}.${month}`;
}
