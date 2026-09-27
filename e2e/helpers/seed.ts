// Deterministic values from the dev seeder (apps/api/Data/Seed/DevSeeder.cs, `dotnet run -- seed`).
// Scores are computed by the real ScoringEngine and decay as the RescoringJob ages permits, so
// specs assert titles, ids, counts and signal rows — never exact score values.
export const seedId = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;

export const ENTITLED_MARKET = { slug: "austin-tx", name: "Austin", city: "Austin", state: "TX" } as const;

/** The seven Austin leads visible to the entitled (Pro, austin-tx only) E2E user. */
export const AUSTIN_LEADS = {
  warehouse: { id: seedId(101), title: "New commercial warehouse with fire sprinkler system" },
  officeAlarm: { id: seedId(102), title: "Office tenant build-out - fire alarm system upgrade" },
  kitchenHood: { id: seedId(103), title: "Restaurant kitchen hood suppression install" },
  failedInspection: { id: seedId(104), title: "Failed sprinkler hydrostatic inspection - correction required" },
  annualInspection: { id: seedId(105), title: "Annual fire inspection - storage facility" },
  closedRetrofit: { id: seedId(106), title: "Completed sprinkler retrofit - closed" },
  dataCenter: { id: seedId(107), title: "New data center clean-agent suppression system" },
} as const;
export const AUSTIN_LEAD_COUNT = Object.keys(AUSTIN_LEADS).length; // 7

/** Leads in markets the entitled user does NOT have. */
export const OTHER_MARKET_LEADS = {
  sanAntonioSprinkler: { id: seedId(201), title: "New distribution center fire sprinkler system", city: "San Antonio" },
  sanAntonioAlarm: { id: seedId(202), title: "Apartment renovation - fire alarm replacement", city: "San Antonio" },
  fortWorthTower: { id: seedId(301), title: "New mixed-use tower fire sprinkler rough-in", city: "Fort Worth" },
} as const;

export const AUSTIN_SOURCE_NAME = "Austin Issued Construction Permits";
export const SAN_ANTONIO_SOURCE_NAME = "San Antonio Permits Issued";

/** Markets with seeded data → the only market pages the marketing site may render. */
export const MARKET_PAGES = {
  austin: "/locations/texas/austin",
  sanAntonio: "/locations/texas/san-antonio",
  fortWorth: "/locations/texas/fort-worth",
} as const;
