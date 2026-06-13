/* PPIQ-PHASE7-PRODUCT */
// Shared Golden-Rule product-page model. Every product page is built from this
// shape so the honesty-lint and the "all sections present" test can assert on it.
export interface ProductCapability { title: string; body: string }
export interface ProductBenefit    { metricLabel: string; body: string }
export interface ProductTier       { name: string; includes: string }

export interface ProductPageModel {
  id: string;
  slug: string;
  name: string;
  category: string;
  headline: string;
  subTagline: string;
  problem: { title: string; body: string };
  capabilities: ProductCapability[];
  benefits: ProductBenefit[];
  diagram: { caption: string; nodes: string[]; note: string };
  licensing: { note: string; tiers: ProductTier[] };
  cta: { heading: string; body: string; buttonLabel: string };
  evidencePosture: string;
}