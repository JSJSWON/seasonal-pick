/**
 * Types and fetch helpers for the SeasonalPick query API.
 *
 * These mirror the API's response shapes. They are written by hand rather
 * than generated: there are five of them, and a generator would be a build
 * step to maintain for no gain at this size.
 *
 * Every call runs on the server, from a server component. That is why no
 * CORS headers are needed on the API, and why the base URL is a plain
 * environment variable rather than a NEXT_PUBLIC_ one - it never reaches the
 * browser.
 */

/** A collection target, as the reader picks it. */
export type TargetSummary = {
  id: number;
  displayName: string;
  categoryName: string;
};

/**
 * A region available for one target.
 *
 * `latestDate` is the most recent survey the API holds for this target here.
 * Some regions stopped being surveyed years ago; the API reports the date
 * instead of hiding them, so the screen decides how to present staleness.
 */
export type RegionSummary = {
  code: string;
  name: string;
  latestDate?: string;
};

/** The most recent surveyed day. Often not today - aT skips weekends and registers late. */
export type LatestPrice = {
  date: string;
  min?: number;
  avg?: number;
  max?: number;
  unit: string;
  unitSize: number;
};

/** One month's average price. `month` is the first day of that month. */
export type MonthlyAverage = {
  month: string;
  average: number;
};

/**
 * How the latest price compares with the same month in past years.
 *
 * When `available` is false there were too few past samples to judge, and
 * the screen must show the price without claiming it is cheap or dear.
 */
export type Verdict = {
  available: boolean;
  sampleSize: number;
  baseline?: number;
  sigma?: number;
  z?: number;
  label: string;
};

/** Everything one screen needs for one target in one region. */
export type PriceDetail = {
  target: TargetSummary;
  region: RegionSummary;
  latest?: LatestPrice;
  monthly: MonthlyAverage[];
  verdict?: Verdict;
};

const API_BASE_URL = process.env.API_BASE_URL ?? "http://localhost:5215";

/**
 * Calls the API and parses the JSON.
 *
 * `no-store` because prices change daily and the page is cheap to render;
 * caching would mostly serve yesterday's survey date.
 *
 * @param path Path beginning with a slash, e.g. "/api/targets".
 * @returns The parsed body.
 * @throws When the API is unreachable or answers with a non-2xx status.
 */
async function fetchJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, { cache: "no-store" });

  if (!response.ok) {
    throw new Error(`API ${response.status} ${response.statusText} for ${path}`);
  }

  return (await response.json()) as T;
}

/** All active targets, in the order the API returns them. */
export function getTargets(): Promise<TargetSummary[]> {
  return fetchJson<TargetSummary[]>("/api/targets");
}

/** The regions this target has prices for. */
export function getRegions(targetId: number): Promise<RegionSummary[]> {
  return fetchJson<RegionSummary[]>(`/api/targets/${targetId}/regions`);
}

/** Trend, latest price and verdict for one target in one region. */
export function getPrices(targetId: number, regionCode: string): Promise<PriceDetail> {
  return fetchJson<PriceDetail>(
    `/api/targets/${targetId}/prices?region=${encodeURIComponent(regionCode)}`,
  );
}
