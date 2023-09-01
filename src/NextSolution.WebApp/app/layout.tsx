import "@/styles/globals.css";

import type { Metadata } from "next";
import fonts from "@/assets/fonts";

import { cn } from "@/lib/utils";

import { App } from "../components/app";

export const metadata: Metadata = {
  title: "Create Next App",
  description: "Generated by create next app"
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={cn(fonts.sansFont.variable)} suppressHydrationWarning>
      <body className="bg-background text-foreground">
        <App>{children}</App>
      </body>
    </html>
  );
}
