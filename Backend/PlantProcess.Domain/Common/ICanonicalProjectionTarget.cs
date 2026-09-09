namespace PlantProcess.Domain.Common;

/// <summary>
/// PPIQ T-253. AN ENTITY A DEFINITION MAY WRITE ITS OUTPUT INTO.
///
/// The product had two different ideas wearing one name. "Canonical entity" means a
/// type the relational model maps - and that set includes import batches, job run
/// history, dashboard widget definitions and connector configuration, none of which is
/// something a plant authors process output into. Treating mapped-ness as eligibility
/// would have offered an author a picker full of the product's own plumbing.
///
/// So eligibility is DECLARED, here, by the Domain that owns the meaning. A mapped
/// entity is not a projection target unless it says it is, which means adding an entity
/// to the model can never silently widen what an author may target.
///
/// It carries no members on purpose. There is nothing an eligible entity must be able
/// to DO; the marker states a fact about the model, and a method here would invite a
/// behaviour that belongs to a service.
///
/// It lives in PlantProcess.Domain because the Domain may not depend on Application.
/// </summary>
public interface ICanonicalProjectionTarget
{
}