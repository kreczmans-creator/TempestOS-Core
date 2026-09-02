using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace;

/// <summary>
/// The canonical engineering Kinds that are durable and rehydratable but
/// have no discipline workspace of their own yet — their vocabulary, and
/// how the product brings them back after a restart.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this class exists.</b> Every other Kind is declared and
/// registered by the discipline registry that also <em>constructs</em> it:
/// <see cref="Mechanical.MechanicalObjectFactoryRegistry"/> owns Part and
/// Assembly, <see cref="Documents.DocumentObjectFactoryRegistry"/> owns
/// Document and Drawing, and so on. The twenty-one Kinds here have no such
/// owner — they are real, compiled, persistable
/// <see cref="EngineeringObjectBase"/> types in <c>Tempest.Core</c> with no
/// factory in front of them. Adding them to a discipline registry would
/// make that registry's own contract false: those classes document their
/// constants as "the Kind this registry can construct", and none of these
/// can be constructed there.
/// </para>
/// <para>
/// <b>The gap this closes.</b> Twelve of them were registered only by
/// <c>Tempest.Samples</c>, so the product's ability to reload a Risk, a
/// Task or a Supplier depended on the sample harness happening to ship
/// (`TD-75`). The other nine were registered nowhere at all: persist one
/// and it came back as an unknown Kind and was dropped. Both are the same
/// defect — a type that implements
/// <see cref="IRehydratable{TSelf}"/> and can be written must have a
/// production rehydrator, or its durability is an accident.
/// </para>
/// <para>
/// <b>Registration is still one boundary (`TD-85`).</b> This class adds no
/// second rehydration mechanism: it is one more caller of the same
/// <see cref="IEngineeringObjectRehydratorRegistry"/> every discipline
/// uses, invoked from the same composition step, and the registry itself
/// rejects a second, different claim on any Kind.
/// </para>
/// <para>
/// <b>These constants are the canonical owner of their values
/// (`ADR-0105`).</b> They were previously string literals inside
/// <c>Tempest.Samples</c>, which is the vocabulary duplication `TD-93`
/// describes. A sample module that creates one of these Kinds should
/// reference the constant here rather than re-spelling it.
/// </para>
/// <para>
/// As each of these Kinds gains a real discipline workspace, its constant
/// and its registration move to that discipline's own registry — the same
/// way every Kind already there arrived. This class shrinks; it is not a
/// permanent home.
/// </para>
/// </remarks>
public static class CanonicalObjectKinds
{
    // ---- Portfolio and programme ------------------------------------

    /// <summary>A portfolio of programmes — the top of the delivery hierarchy.</summary>
    public const string Portfolio = "Portfolio";

    /// <summary>A programme of projects within a portfolio.</summary>
    public const string Programme = "Programme";

    // ---- Governance, risk and decisions -----------------------------

    /// <summary>An identified risk, with likelihood and severity.</summary>
    public const string Risk = "Risk";

    /// <summary>A safety hazard — a <see cref="Risk"/> specialisation, and its own Kind.</summary>
    public const string Hazard = "Hazard";

    /// <summary>An issue raised against the engineering work.</summary>
    public const string Issue = "Issue";

    /// <summary>A recorded engineering decision.</summary>
    public const string Decision = "Decision";

    /// <summary>A recorded assumption the engineering work depends on.</summary>
    public const string Assumption = "Assumption";

    // ---- Work, planning and delivery --------------------------------

    /// <summary>An engineering task.</summary>
    public const string Task = "Task";

    /// <summary>An action arising from a review or meeting — a <see cref="Task"/> specialisation, and its own Kind.</summary>
    public const string Action = "Action";

    /// <summary>A programme or project milestone.</summary>
    public const string Milestone = "Milestone";

    /// <summary>A deliverable due against a milestone.</summary>
    public const string Deliverable = "Deliverable";

    // ---- Change and process -----------------------------------------

    /// <summary>A request for an engineering change.</summary>
    public const string ChangeRequest = "ChangeRequest";

    /// <summary>An engineering change being carried out.</summary>
    public const string EngineeringChange = "EngineeringChange";

    /// <summary>A formal approval record.</summary>
    public const string Approval = "Approval";

    /// <summary>A formal review record.</summary>
    public const string Review = "Review";

    // ---- Supply chain and integration -------------------------------

    /// <summary>A supplier.</summary>
    public const string Supplier = "Supplier";

    /// <summary>An item on a purchase order.</summary>
    public const string PurchaseItem = "PurchaseItem";

    /// <summary>A link to an object held in an external system.</summary>
    public const string ExternalSystemLink = "ExternalSystemLink";

    // ---- Analysis and verification ----------------------------------

    /// <summary>An engineering simulation.</summary>
    public const string Simulation = "Simulation";

    /// <summary>A test — a <see cref="VerificationActivity"/> specialisation, and its own Kind.</summary>
    public const string Test = "Test";

    /// <summary>The bare verification marker Kind (`WP 8.2C`), distinct from <c>VerificationActivity</c>.</summary>
    public const string Verification = "Verification";

    /// <summary>Every Kind this class declares, in declaration order.</summary>
    /// <remarks>
    /// Exposed so a test can assert the production registration set
    /// against it directly, rather than restating the list and drifting
    /// from it.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } =
    [
        Portfolio, Programme,
        Risk, Hazard, Issue, Decision, Assumption,
        Task, Action, Milestone, Deliverable,
        ChangeRequest, EngineeringChange, Approval, Review,
        Supplier, PurchaseItem, ExternalSystemLink,
        Simulation, Test, Verification,
    ];

    /// <summary>Registers how each Kind above comes back after a restart (`TD-85`).</summary>
    /// <remarks>
    /// One line per Kind, exactly as every discipline registry does. The
    /// concrete type is the type the Kind comes back as; the state that
    /// distinguishes one instance from another belongs to each type in
    /// <c>Tempest.Core</c>, and nothing about any type's own fields is
    /// known here.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void RegisterRehydrators(IEngineeringObjectRehydratorRegistry registry, EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);

        registry.Register<Core.EngineeringDomain.Portfolio>(Portfolio, context);
        registry.Register<Core.EngineeringDomain.Programme>(Programme, context);

        registry.Register<Core.EngineeringDomain.Risk>(Risk, context);
        registry.Register<Core.EngineeringDomain.Hazard>(Hazard, context);
        registry.Register<Core.EngineeringDomain.Issue>(Issue, context);
        registry.Register<Core.EngineeringDomain.Decision>(Decision, context);
        registry.Register<Core.EngineeringDomain.Assumption>(Assumption, context);

        registry.Register<EngineeringTask>(Task, context);
        registry.Register<EngineeringAction>(Action, context);
        registry.Register<Core.EngineeringDomain.Milestone>(Milestone, context);
        registry.Register<Core.EngineeringDomain.Deliverable>(Deliverable, context);

        registry.Register<Core.EngineeringDomain.ChangeRequest>(ChangeRequest, context);
        registry.Register<Core.EngineeringDomain.EngineeringChange>(EngineeringChange, context);
        registry.Register<Core.EngineeringDomain.Approval>(Approval, context);
        registry.Register<Core.EngineeringDomain.Review>(Review, context);

        registry.Register<Core.EngineeringDomain.Supplier>(Supplier, context);
        registry.Register<Core.EngineeringDomain.PurchaseItem>(PurchaseItem, context);
        registry.Register<Core.EngineeringDomain.ExternalSystemLink>(ExternalSystemLink, context);

        registry.Register<Core.EngineeringDomain.Simulation>(Simulation, context);
        registry.Register<Core.EngineeringDomain.Test>(Test, context);
        registry.Register<Core.EngineeringDomain.Verification>(Verification, context);
    }
}
