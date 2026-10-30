import Link from "next/link";

/** One choice in a picker: where it goes and how it reads. */
export type PickerOption = {
  key: string;
  href: string;
  label: string;
  /** Shown under the label, e.g. a category name or a last-surveyed date. */
  note?: string;
  selected: boolean;
};

/**
 * A row of choices rendered as links.
 *
 * Links, not buttons, because the whole selection lives in the URL. That
 * keeps the page a server component with no client state to go stale, makes
 * the back button and a shared link work, and means the API never has to
 * answer a request from the browser.
 */
export function Picker({
  legend,
  options,
}: {
  legend: string;
  options: PickerOption[];
}) {
  return (
    <section>
      <h2 className="mb-2 text-sm font-medium text-neutral-500">{legend}</h2>
      <ul className="flex flex-wrap gap-2">
        {options.map((option) => (
          <li key={option.key}>
            <Link
              href={option.href}
              aria-current={option.selected ? "true" : undefined}
              className={
                option.selected
                  ? "block rounded-lg border border-emerald-600 bg-emerald-600 px-3 py-2 text-white"
                  : "block rounded-lg border border-neutral-300 bg-white px-3 py-2 text-neutral-800 hover:border-neutral-400"
              }
            >
              <span className="block text-sm">{option.label}</span>
              {option.note && (
                <span
                  className={
                    option.selected
                      ? "block text-xs text-emerald-100"
                      : "block text-xs text-neutral-500"
                  }
                >
                  {option.note}
                </span>
              )}
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
