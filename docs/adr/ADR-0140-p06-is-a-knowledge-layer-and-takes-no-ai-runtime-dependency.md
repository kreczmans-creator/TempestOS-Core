# ADR-0140: P06 Is a Knowledge Layer and Takes No AI Runtime Dependency

## Status

Accepted — `Group F` (P06 AI Knowledge & Academy), 2026-09-06.

## Context

`P06` is named "AI Knowledge & Academy" and holds a prompt library. The
obvious next step — and the one every comparable system takes — is to
execute those prompts: bind a model, add a client, offer
`IPromptExecutor.ExecuteAsync`, and the library becomes useful
immediately.

Three reasons not to, in increasing order of importance.

**Coupling.** A model binding is a provider dependency, an authentication
story, a network dependency, a cost model and a rate limit, all of which
change on the provider's schedule rather than the platform's. A library of
governed text has none of that and stays useful for decades.

**Governance.** A prompt is knowledge about how to ask for something
well, and it improves the way a template improves — by revision, with
review, with the earlier version still readable. Execution is a different
concern with a different lifecycle, and joining them means the prompt
library's governance is only as good as the runtime's.

**What a prompt library is for.** The value is that an organisation can
see, review and improve the instructions it relies on. That value is
entirely present without execution, and an execution path added
prematurely makes the library a configuration file for a runtime rather
than a body of knowledge.

## Decision

**No type under `Tempest.Core.Knowledge` executes anything.** Enforced by
a reflection test over every type in the namespace, rejecting any public
method whose name begins with `Execute`, `Invoke`, `Run`, `Complete`,
`Generate`, `Infer`, `Grade`, `Score`, `Mark`, `Chat` or `Ask`.

The guard matches a **leading verb followed by an uppercase letter**, not
the fragment anywhere. The first version matched anywhere and flagged
`IsMachineGenerated` and `EvaluateDuplicateReferences`; a guard that cries
wolf is a guard somebody deletes. `Evaluate` is deliberately not on the
list — validating a record is exactly what these services do — and the
grading concern is caught by `Grade` and `Score`. The guard's own
behaviour is asserted by a test.

**No model or provider assembly is referenced.** A second test asserts
that `Tempest.Core`'s referenced assemblies include nothing matching
OpenAI, Anthropic, Azure.AI, Microsoft.ML, SemanticKernel, LangChain or
HuggingFace.

**No executor, agent, runner or model client is registered.** A host test
composes the real `TempestHost` and asserts that no type in the namespace
is named for one.

**`PromptRecord.OutputRequiresHumanReview` is unconditionally `true`**,
and a prompt that does not say what a person must check before relying on
its output is a validation **error**. A prompt whose instruction asks for
something to be approved, certified or signed off is reported: a prompt
may ask for an assessment and must not ask for the act.

## Consequences

**The prompt library does nothing on its own**, and a caller wanting
execution must build it outside `P06`. That is the intended seam: the
knowledge stays governed here, and whatever runs it is somebody else's
lifecycle.

**`P06` cannot regress into an agent framework by accident.** Adding one
requires deleting a test that says why it should not exist, which is a
conversation rather than a commit.

**The guard will occasionally be wrong** about a legitimate name, and the
remedy is to argue with it in a pull request rather than to loosen it.
That is the correct cost.
