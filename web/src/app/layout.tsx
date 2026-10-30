import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "seasonal-pick",
  description: "농수산물 가격이 평년보다 싼지 비싼지 알려줍니다.",
};

/** Root layout. Korean content, so the document language is set accordingly. */
export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ko">
      <body className="bg-neutral-50 text-neutral-900 antialiased">
        {children}
      </body>
    </html>
  );
}
