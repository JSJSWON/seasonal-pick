import Link from "next/link";

/**
 * Page frame shared by the recommendation list and the item detail.
 *
 * The title links home so the detail page has a way back that does not rely
 * on the browser's back button.
 */
export function Shell({ children }: { children: React.ReactNode }) {
  return (
    <main className="mx-auto max-w-3xl px-4 py-10">
      <header className="mb-8">
        <Link href="/" className="inline-block">
          <h1 className="text-2xl font-semibold tracking-tight text-neutral-900">
            seasonal-pick
          </h1>
        </Link>
        <p className="mt-1 text-neutral-600">
          이 가격이 평소보다 싼지 알려줍니다.
        </p>
      </header>
      {children}
    </main>
  );
}

/**
 * Shown when the API cannot be reached.
 *
 * Every page fails the same way, and a blank screen would not tell a reader
 * whether the data is missing or the service is down.
 */
export function ApiUnavailable() {
  return (
    <Shell>
      <div className="rounded-xl border border-amber-300 bg-amber-50 p-5">
        <p className="font-medium text-amber-900">조회 API에 연결할 수 없습니다.</p>
        <p className="mt-1 text-sm text-amber-800">
          <code className="rounded bg-amber-100 px-1">
            cd api/SeasonalPick.Api &amp;&amp; dotnet run
          </code>{" "}
          으로 띄운 뒤 새로고침하세요. 다른 주소를 쓰면{" "}
          <code className="rounded bg-amber-100 px-1">API_BASE_URL</code>을
          설정하세요.
        </p>
      </div>
    </Shell>
  );
}
