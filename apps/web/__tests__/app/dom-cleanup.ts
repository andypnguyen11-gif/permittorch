// Vitest runs without `globals: true` here, so Testing Library cannot register
// its automatic afterEach cleanup. Import this from every DOM test in this
// folder so renders never leak between tests.
import { afterEach } from "vitest";
import { cleanup } from "@testing-library/react";

afterEach(() => cleanup());
