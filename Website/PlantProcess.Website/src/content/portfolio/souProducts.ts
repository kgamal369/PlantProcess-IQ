/* PPIQ-T069-PORTFOLIO */
// =============================================================================
// THE SOU PRODUCT PORTFOLIO - the single authority for the Products mega-menu,
// the /products portfolio page, the canonical product routes and the validator.
//
// Chapter 6 6.2.1: SOU Industrial Software has FIVE SEPARATE PRODUCTS -
// PlantProcess IQ, MES, QES, Yard and Warehouse Management, Energy Management.
// PPIQ is the flagship. PPIQ IS NOT THE COMPANY AND IS NOT A CONTAINER AROUND
// THE OTHER FOUR. None of the four is a grouping of flagship capabilities.
//
// Chapter 6 6.2.14, one registry one truth: a product in this file appears on
// every surface automatically; a product absent from this file does not exist
// on the site. No surface may keep its own product list.
//
// WHY THIS FILE IS SEPARATE FROM content/products/*.ts
// Those modules describe PPIQ integration and capability content - mes.ts is
// literally "We read your MES. We don't replace it.", which is PPIQ's stance
// toward a customer's existing MES, not an MES product. They remain available to
// the PPIQ page and are deliberately NOT repurposed here.
//
// THE HONESTY RULE, Chapter 6 6.2.10
// PPIQ claims are traceable to Chapters 1 to 5. The other four have no
// equivalent Master Design chapter yet, so their wording is TARGET DESIGN - what
// the product does by design - and never a present-tense implementation claim.
// No result figure, no certification, no named customer, no claim of control
// over plant equipment.
// =============================================================================

export type ClaimBasis = "traceable" | "target-design";

export type StackLayer =
  | "Plant intelligence"
  | "Plant execution"
  | "Material flow"
  | "Resource efficiency";

export interface PortfolioRelationship {
  /** Canonical slug of the sibling product. */
  slug: string;
  /** How the two products relate commercially and operationally. */
  statement: string;
  /** Whether that relationship exists today or is a design intent. */
  status: "Implemented" | "Target Product Design";
}

export interface PortfolioProduct {
  id: string;
  /** Canonical route segment. Chapter 6.2.12. */
  slug: string;
  /** Legacy or shorthand segments that must redirect to the canonical slug. */
  aliasSlugs: string[];
  /** Full product name. */
  name: string;
  /** Short label for the mega-menu, where five full names do not fit. */
  menuLabel: string;
  /** One line of value proposition, mega-menu and portfolio card. */
  valueLine: string;
  /** lucide-react icon name, resolved by the menu and the portfolio. */
  icon: string;
  /** Exactly one product is the flagship. It is not a parent. */
  isFlagship: boolean;
  /** Commercial position in the SOU stack graphic of 6.2.15. */
  stackLayer: StackLayer;
  /** Governs the wording, per 6.2.10. */
  claimBasis: ClaimBasis;
  /** 6.2.15: the problem this product owns. */
  problemOwned: string;
  /** 6.2.15: who buys it. */
  typicalBuyer: string;
  /** 6.2.15: where it sits operationally. */
  operationalPosition: string;
  /** 6.2.15: primary benefits, stated without invented figures. */
  primaryBenefits: string[];
  /** 6.2.15: what it does INDEPENDENTLY of the other four. */
  independence: string;
  /** 6.2.15: its relationship with the other four. */
  relationships: PortfolioRelationship[];
}

const PLANTPROCESS_IQ: PortfolioProduct = {
  id: "plantprocess-iq",
  slug: "plantprocess-iq",
  aliasSlugs: ["ppiq", "platform"],
  name: "PlantProcess IQ",
  menuLabel: "PlantProcess IQ",
  valueLine: "Plant intelligence",
  icon: "Activity",
  isFlagship: true,
  stackLayer: "Plant intelligence",
  claimBasis: "traceable",
  problemOwned:
    "Process and quality data already exist across level 2 systems, laboratories, inspection and " +
    "spreadsheets, but nobody can trace a defect back through the process that produced it.",
  typicalBuyer:
    "Head of quality, head of process engineering, and the plant IT function that has to approve " +
    "how a supplier connects.",
  operationalPosition:
    "Above the execution layer. It reads from the systems that already run the plant and writes " +
    "nothing back to them.",
  primaryBenefits: [
    "Genealogy from raw material to finished unit, so a quality event can be traced to the process that caused it.",
    "Statistical and machine learning analysis that states its confidence and refuses when the data does not support an answer.",
    "Evidence for every finding, so a conclusion can be checked rather than believed.",
    "No-code authoring, so an engineer builds an analysis without waiting for a developer.",
  ],
  independence:
    "Runs against a customer's existing systems on its own. It requires no other SOU product.",
  relationships: [
    {
      slug: "mes",
      statement:
        "Where a customer runs SOU MES, PlantProcess IQ can read its execution record as one more source.",
      status: "Target Product Design",
    },
    {
      slug: "qes",
      statement:
        "Where a customer runs SOU QES, the quality record it governs is a source PlantProcess IQ can analyse.",
      status: "Target Product Design",
    },
    {
      slug: "yard-warehouse-management",
      statement:
        "Location and movement history is a further dimension PlantProcess IQ can correlate against quality.",
      status: "Target Product Design",
    },
    {
      slug: "energy-management",
      statement:
        "Consumption per unit is a further dimension PlantProcess IQ can correlate against process and quality.",
      status: "Target Product Design",
    },
  ],
};

const MES: PortfolioProduct = {
  id: "mes",
  slug: "mes",
  aliasSlugs: ["manufacturing-execution", "manufacturing-execution-system"],
  name: "Manufacturing Execution System",
  menuLabel: "Manufacturing Execution",
  valueLine: "Production executed",
  icon: "Factory",
  isFlagship: false,
  stackLayer: "Plant execution",
  claimBasis: "target-design",
  problemOwned:
    "The order that was planned and the work that was actually performed drift apart, and the " +
    "record of what happened on the floor is assembled after the fact.",
  typicalBuyer:
    "Production manager and operations director, with the plant IT function on the approval path.",
  operationalPosition:
    "The execution layer itself. It is designed to carry the order, the route and the record of " +
    "work performed.",
  primaryBenefits: [
    "By design, the executed route is recorded as it happens rather than reconstructed later.",
    "By design, order progress is visible against plan without a manual status call.",
    "By design, the execution record is structured so downstream analysis can consume it.",
  ],
  independence:
    "Designed to be bought and run on its own, as the execution system of a plant that has none. " +
    "It is not a component of the flagship.",
  relationships: [
    {
      slug: "plantprocess-iq",
      statement:
        "Its execution record is designed to be readable by PlantProcess IQ, which analyses it rather than replacing it.",
      status: "Target Product Design",
    },
    {
      slug: "qes",
      statement:
        "Execution and quality govern the same unit of production and are designed to share its identity.",
      status: "Target Product Design",
    },
  ],
};

const QES: PortfolioProduct = {
  id: "qes",
  slug: "qes",
  aliasSlugs: ["quality-execution", "quality-execution-system"],
  name: "Quality Execution System",
  menuLabel: "Quality Execution System",
  valueLine: "Quality governed and recorded",
  icon: "ScanLine",
  isFlagship: false,
  stackLayer: "Plant execution",
  claimBasis: "target-design",
  problemOwned:
    "Quality decisions - hold, release, downgrade, concession - are taken across inspection, the " +
    "laboratory and the shift office, and the reasoning behind them is not held in one governed record.",
  typicalBuyer:
    "Quality manager and the certification function answerable for what was released.",
  operationalPosition:
    "The execution layer, alongside production. It is designed to own the quality decision and its record.",
  primaryBenefits: [
    "By design, a quality decision carries who took it, on what evidence and under which specification.",
    "By design, inspection and laboratory results arrive against the same unit rather than into separate systems.",
    "By design, the release record is complete enough to answer a customer complaint without reconstruction.",
  ],
  independence:
    "Designed to be bought and run on its own by a plant that needs governed quality execution. " +
    "It is not a quality feature of an analytics product.",
  relationships: [
    {
      slug: "mes",
      statement:
        "Quality and execution govern the same unit of production and are designed to share its identity.",
      status: "Target Product Design",
    },
    {
      slug: "plantprocess-iq",
      statement:
        "The governed quality record is designed to be a source PlantProcess IQ can analyse for cause.",
      status: "Target Product Design",
    },
  ],
};

const YARD_WAREHOUSE: PortfolioProduct = {
  id: "yard-warehouse-management",
  slug: "yard-warehouse-management",
  aliasSlugs: ["yard", "warehouse", "yard-warehouse"],
  name: "Yard and Warehouse Management",
  menuLabel: "Yard & Warehouse",
  valueLine: "Material located and moved",
  icon: "Warehouse",
  isFlagship: false,
  stackLayer: "Material flow",
  claimBasis: "target-design",
  problemOwned:
    "Material is somewhere in a yard, a bay or a warehouse, and finding it, moving it and knowing " +
    "how long it has been there depends on people who happen to remember.",
  typicalBuyer:
    "Logistics and warehouse manager, and the operations director answerable for despatch commitments.",
  operationalPosition:
    "The material flow layer, between production and despatch. It is designed to manage location " +
    "and movement, not only to display them.",
  primaryBenefits: [
    "By design, every unit has a current location and a movement history rather than a last known sighting.",
    "By design, storage and retrieval are directed rather than improvised.",
    "By design, dwell time is a managed quantity rather than something discovered at despatch.",
  ],
  independence:
    "Designed to be bought and run on its own by a site whose material flow is the constraint. " +
    "It is a management product, not a read-only view inside another product.",
  relationships: [
    {
      slug: "plantprocess-iq",
      statement:
        "Location and dwell history are designed to be available to PlantProcess IQ as an analysable dimension.",
      status: "Target Product Design",
    },
    {
      slug: "mes",
      statement:
        "Movement and execution refer to the same unit of production and are designed to share its identity.",
      status: "Target Product Design",
    },
  ],
};

const ENERGY: PortfolioProduct = {
  id: "energy-management",
  slug: "energy-management",
  aliasSlugs: ["energy", "energy-management-system", "ems"],
  name: "Energy Management System",
  menuLabel: "Energy Management",
  valueLine: "Consumption understood",
  icon: "Zap",
  isFlagship: false,
  stackLayer: "Resource efficiency",
  claimBasis: "target-design",
  problemOwned:
    "Energy is measured at the meter and paid for at the site, but nobody can say what a single " +
    "order, product or shift actually consumed.",
  typicalBuyer:
    "Energy manager and the finance function carrying the tariff, with operations on the approval path.",
  operationalPosition:
    "The resource efficiency layer, across the whole plant rather than one process step.",
  primaryBenefits: [
    "By design, consumption is attributed to a unit of production rather than only to a period.",
    "By design, demand is visible against the tariff structure that will be billed.",
    "By design, the consumption record is structured so it can be analysed against process conditions.",
  ],
  independence:
    "Designed to be bought and run on its own by a site whose energy cost is the pressure. " +
    "It is not an energy feature inside an analytics product.",
  relationships: [
    {
      slug: "plantprocess-iq",
      statement:
        "Consumption per unit is designed to be available to PlantProcess IQ as an analysable dimension.",
      status: "Target Product Design",
    },
    {
      slug: "mes",
      statement:
        "Consumption and execution refer to the same unit of production and are designed to share its identity.",
      status: "Target Product Design",
    },
  ],
};

/**
 * The five standalone products, in portfolio order. PlantProcess IQ is first
 * because it is the flagship, NOT because the other four sit underneath it.
 */
export const souProducts: PortfolioProduct[] = [
  PLANTPROCESS_IQ,
  MES,
  QES,
  YARD_WAREHOUSE,
  ENERGY,
];

/** The canonical route for a product. Never build this string by hand elsewhere. */
export function productPath(product: PortfolioProduct): string {
  return "/products/" + product.slug;
}

/** Every alias segment mapped to its canonical path, for the compatibility redirects. */
export const productAliasRedirects: Record<string, string> = souProducts.reduce(
  (acc, product) => {
    for (const alias of product.aliasSlugs) acc[alias] = productPath(product);
    return acc;
  },
  {} as Record<string, string>,
);

/** The flagship, resolved from the data rather than hard-coded at each call site. */
export const flagshipProduct: PortfolioProduct =
  souProducts.find((product) => product.isFlagship) ?? souProducts[0];

export function findProductBySlug(slug: string | undefined): PortfolioProduct | undefined {
  if (!slug) return undefined;
  return souProducts.find((product) => product.slug === slug);
}

/** The SOU Industrial Software Stack of 6.2.15, in display order. */
export const stackLayers: StackLayer[] = [
  "Plant intelligence",
  "Plant execution",
  "Material flow",
  "Resource efficiency",
];