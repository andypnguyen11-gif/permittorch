"use client";
import { useRouter } from "next/navigation";
import { LogOut, Settings } from "lucide-react";
import { signOut } from "firebase/auth";
import { toast } from "sonner";
import { firebaseAuth } from "@/lib/firebase/client";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

function initials(email: string): string {
  const name = email.split("@")[0] ?? "";
  const parts = name.split(/[._-]+/).filter(Boolean);
  const letters = parts.length >= 2 ? parts[0][0] + parts[1][0] : name.slice(0, 2);
  return letters.toUpperCase() || "?";
}

export function AccountMenu({ email }: { email: string }) {
  const router = useRouter();

  async function handleSignOut() {
    try {
      await signOut(firebaseAuth);
      await fetch("/api/logout");
      router.push("/login");
    } catch {
      toast.error("Could not sign out. Please try again.");
    }
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Account menu"
        className="flex size-9 items-center justify-center rounded-full bg-orange-100 text-xs font-semibold text-orange-700 ring-1 ring-orange-200 transition-colors outline-none hover:bg-orange-200 focus-visible:ring-3 focus-visible:ring-ring/50"
      >
        {initials(email)}
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        <div className="px-2 py-1.5">
          <p className="text-xs text-muted-foreground">Signed in as</p>
          <p className="truncate text-sm font-medium">{email}</p>
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => router.push("/app/account")}>
          <Settings aria-hidden /> Account settings
        </DropdownMenuItem>
        <DropdownMenuItem variant="destructive" onClick={handleSignOut}>
          <LogOut aria-hidden /> Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
