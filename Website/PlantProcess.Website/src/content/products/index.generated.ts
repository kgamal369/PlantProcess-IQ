/* PPIQ-PHASE7-PRODUCTS */
// Aggregated Phase-7 product registry. Import { phase7Products } to render or
// route them; import individual products by name if you prefer.
import { yardWarehouseProduct } from "./yardWarehouse";
import { mesProduct } from "./mes";
import type { ProductPageModel } from "./model";

export const phase7Products: ProductPageModel[] = [yardWarehouseProduct, mesProduct];
export { yardWarehouseProduct, mesProduct };
export type { ProductPageModel };