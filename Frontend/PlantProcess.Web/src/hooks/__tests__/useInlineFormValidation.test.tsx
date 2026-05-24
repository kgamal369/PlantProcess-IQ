import { renderHook, act } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import {
  useInlineFormValidation,
  validateCode,
  validatePort,
  validateRequired,
} from "@/hooks/useInlineFormValidation";

type Form = {
  code: string;
  name: string;
  port: string;
};

type Field = "code" | "name" | "port";

describe("useInlineFormValidation", () => {
  it("returns inline errors only after touch or submit", () => {
    const form: Form = {
      code: "",
      name: "",
      port: "99999",
    };

    const { result } = renderHook(() =>
      useInlineFormValidation<Form, Field>(form, (value) => ({
        code: validateCode(value.code, "Code"),
        name: validateRequired(value.name, "Name"),
        port: validatePort(value.port),
      }))
    );

    expect(result.current.getError("code")).toBeUndefined();

    act(() => result.current.markTouched("code"));

    expect(result.current.getError("code")).toMatch(/required/i);

    act(() => {
      const ok = result.current.prepareSubmit();
      expect(ok).toBe(false);
    });

    expect(result.current.getError("port")).toMatch(/between 1 and 65535/i);
  });
});