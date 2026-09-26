import type { FieldErrors, FieldValues, Resolver } from "react-hook-form";
import type { z } from "zod";

// Minimal react-hook-form resolver for zod (@hookform/resolvers is not a
// dependency and package.json is frozen). Reports the first issue per field.
export function zodResolver<T extends FieldValues>(schema: z.ZodType<T>): Resolver<T> {
  return async (values) => {
    const result = schema.safeParse(values);
    if (result.success) return { values: result.data, errors: {} };
    const errors: Record<string, { type: string; message: string }> = {};
    for (const issue of result.error.issues) {
      const key = issue.path.join(".");
      if (!errors[key]) errors[key] = { type: issue.code, message: issue.message };
    }
    return { values: {}, errors: errors as FieldErrors<T> };
  };
}
