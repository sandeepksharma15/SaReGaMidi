# SaReGaMidi coding style

Apply this guide to production code, tests, examples, and project files. Use the root `.editorconfig` for mechanical formatting.

## Layout and names

- Use four spaces, file-scoped namespaces, using directives outside namespaces, and Allman braces for block bodies. A clear single-statement guard may omit braces if its body is on the next line.
- Put one statement or declaration on each line. Use white space to separate members and logical steps. Wrap long signatures, calls, and initializers at logical boundaries.
- Use explicit local types by default. Allow `var` when the type is immediately obvious from the initializer or when an anonymous type requires it. Use target-typed `new()` when the declared type is clear.
- Use descriptive names, PascalCase for types and members, camelCase for parameters and locals, and `_camelCase` for private and protected backing and dependency fields. Prefix interfaces with `I`.
- In larger classes, use `Fields`, `Constructors`, `Properties`, and `Methods` regions in that order. Omit empty regions and group related members together.

## Implementation

- Keep each file focused. Prefer early guards and straightforward control flow. Use expression bodies for simple forwarding methods or calculations and block bodies for multi-step work.
- Keep dependencies explicit. Use modern C# features when they improve readability. Introduce abstractions to clarify responsibilities or remove meaningful duplication.
- Keep asynchronous I/O asynchronous and pass cancellation through where supported.
- Comment only when a reason, invariant, or ownership rule needs explanation. Add brief XML documentation where a public contract needs clarification.

## Validation

- When tests are added, prefer xUnit. Use behavior-oriented names such as `Method_Scenario_ExpectedBehavior` and separate Arrange, Act, and Assert with blank lines.
- Build the affected solution and exercise changed behavior. Review the diff for unintended changes and unrelated formatting.
