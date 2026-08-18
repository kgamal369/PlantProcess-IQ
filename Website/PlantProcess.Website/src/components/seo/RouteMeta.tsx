// ROUTE-LEVEL METADATA.
//
// souindustrial.com is a COMPANY domain. Its root must describe SOU Industrial
// Software and its five products; the flagship must keep its own identity on
// its own route.
//
// This site had no per-route metadata at all - a single static index.html title
// served every path - so correcting index.html alone would have made the
// PlantProcess IQ product page describe the company. This component supplies
// the missing mechanism: one map, applied on navigation, falling back to the
// corporate default for any route not listed.
//
// It renders nothing. It only sets what a browser tab and a link preview show.

import { useEffect } from "react";
import { useLocation } from "react-router-dom";

type Meta = { title: string; description: string; ogTitle?: string };

const CORPORATE: Meta = {
  title: "SOU Industrial Software | Industrial Software for Manufacturing",
  description:
    "SOU Industrial Software develops specialised industrial software for plant intelligence, "
    + "manufacturing execution, quality execution, material flow and energy management.",
};

/** Longest-prefix match, so /products/plantprocess-iq/anything stays on the
 *  product's own identity rather than falling back to the company's. */
const ROUTES: Array<[string, Meta]> = [
  ["/products/plantprocess-iq", {
    title: "PlantProcess IQ | Plant Intelligence | SOU Industrial Software",
    ogTitle: "PlantProcess IQ | Stop the Losses",
    description:
      "PlantProcess IQ is a read-only, evidence-grade process-to-quality intelligence layer for "
      + "manufacturing plants. Connect fragmented plant data, reconstruct the production journey, "
      + "and identify evidence-ranked contributors to quality and performance loss.",
  }],
  ["/products", {
    title: "Products | SOU Industrial Software",
    description:
      "Five specialised industrial software products: plant intelligence, manufacturing execution, "
      + "quality execution, material flow and energy management. Each is sold and used independently.",
  }],
];

function metaFor(pathname: string): Meta {
  let best: Meta = CORPORATE;
  let bestLength = -1;

  for (const [prefix, meta] of ROUTES) {
    if ((pathname === prefix || pathname.startsWith(prefix + "/")) && prefix.length > bestLength) {
      best = meta;
      bestLength = prefix.length;
    }
  }

  return best;
}

function setMeta(selector: string, attribute: string, value: string) {
  const node = document.head.querySelector(selector);
  if (node) node.setAttribute(attribute, value);
}

export function RouteMeta() {
  const { pathname } = useLocation();

  useEffect(() => {
    const meta = metaFor(pathname);
    document.title = meta.title;
    setMeta('meta[name="description"]', "content", meta.description);
    setMeta('meta[property="og:title"]', "content", meta.ogTitle ?? meta.title);
    setMeta('meta[property="og:description"]', "content", meta.description);
  }, [pathname]);

  return null;
}

export default RouteMeta;
