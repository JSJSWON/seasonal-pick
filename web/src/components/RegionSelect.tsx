"use client";

import { useRouter } from "next/navigation";

/** One region in the dropdown. */
export type RegionOption = {
  code: string;
  name: string;
};

/**
 * A dropdown for choosing the region.
 *
 * A client component only because a change event needs a handler; the
 * selection itself still lives in the URL, so the pages stay server
 * components and the back button and shared links keep working.
 *
 * @param basePath Page to navigate to, without the query, e.g. "/" or "/items/3".
 * @param options Regions to offer.
 * @param selectedCode Code of the region currently shown.
 */
export function RegionSelect({
  basePath,
  options,
  selectedCode,
}: {
  basePath: string;
  options: RegionOption[];
  selectedCode: string;
}) {
  const router = useRouter();

  return (
    <div>
      <label
        htmlFor="region"
        className="mb-2 block text-sm font-medium text-neutral-500"
      >
        지역
      </label>
      <select
        id="region"
        value={selectedCode}
        onChange={(event) =>
          router.push(`${basePath}?region=${event.target.value}`)
        }
        className="rounded-lg border border-neutral-300 bg-white px-3 py-2 text-neutral-800"
      >
        {options.map((option) => (
          <option key={option.code} value={option.code}>
            {option.name}
          </option>
        ))}
      </select>
    </div>
  );
}
