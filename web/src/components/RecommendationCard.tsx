import Link from "next/link";
import type { Recommendation } from "@/lib/api";
import { formatPrice, formatUnit } from "@/lib/format";

/**
 * One recommended item, linking to its detail page.
 *
 * Both the verdict and the percentage are shown. z decides the ranking
 * because it accounts for how much an item normally moves, but a reader
 * cannot feel a z - "35% cheaper" is what tells them whether to bother.
 */
export function RecommendationCard({
  recommendation,
  regionCode,
}: {
  recommendation: Recommendation;
  regionCode: string;
}) {
  const { target, latest, verdict } = recommendation;

  return (
    <li>
      <Link
        href={`/items/${target.id}?region=${regionCode}`}
        className="block rounded-xl border border-neutral-200 bg-white p-5 hover:border-neutral-400"
      >
        <p className="text-sm text-neutral-500">{target.categoryName}</p>
        <p className="mt-1 text-lg font-semibold text-neutral-900">
          {target.displayName}
        </p>

        <p className="mt-3 text-2xl font-semibold tracking-tight text-neutral-900">
          {formatPrice(latest.avg!)}
          <span className="ml-1 text-sm font-normal text-neutral-500">
            / {formatUnit(latest.unit, latest.unitSize)}
          </span>
        </p>

        <p className="mt-2 text-emerald-700">
          {verdict.label}
          {verdict.differencePercent !== undefined && (
            <span className="ml-2 text-sm text-neutral-600">
              평년 대비 {verdict.differencePercent}%
            </span>
          )}
        </p>
      </Link>
    </li>
  );
}
