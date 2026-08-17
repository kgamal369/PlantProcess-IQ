// T-051. ONE PRESENTER FOR THE SEVEN CANONICAL WIDGET STATES.
//
// The state is NOT decided here. resolveAuthoringState and describeAuthoringState
// from T-040 are the authority: this component asks them and renders the answer.
// No second union, no second precedence, no second tone table.
//
// Wording. The descriptor's sentence is kept verbatim for failed, refused and
// blocked, because those carry the server's own reason. Only loading, empty and
// filtered-empty are re-worded, because the authoring sentences there speak of a
// "definition" being "run", which is not what a viewer is looking at. The
// empty / filtered-empty distinction is preserved word for word: empty says
// there is nothing here, filtered-empty says this selection matched nothing.

import { describeAuthoringState, type AuthoringState, type AuthoringStateFacts } from "@/authoring/authoringStates";
import "./WidgetStatePanel.css";

type Wording = { heading: string; sentence: string; nextAction: string | null };

const WIDGET_WORDING: Partial<Record<AuthoringState, Wording>> = {
  loading: {
    heading: "Running",
    sentence: "This widget is fetching its result.",
    nextAction: null,
  },
  empty: {
    heading: "No rows",
    sentence:
      "This widget returned nothing, and no filter is narrowing it, so there is"
      + " nothing here to show.",
    nextAction: null,
  },
  "filtered-empty": {
    heading: "No rows matched",
    sentence:
      "This widget ran and returned nothing. The current selection narrowed the"
      + " result away, so this is an answer about scope rather than an absence of"
      + " data.",
    nextAction: "Widen or clear a filter, then look again.",
  },
};

/** Facts for a widget that came apart while rendering. Deliberately carries no
 *  exception text: the boundary's diagnostics beacon already has it. */
export const WIDGET_RENDER_FAILURE_FACTS: AuthoringStateFacts = {
  running: false,
  failure: "This widget could not be displayed.",
  refusal: null,
  blocker: null,
  rowCount: null,
  filtered: false,
};

export function WidgetStatePanel({ facts }: { facts: AuthoringStateFacts }) {
  const descriptor = describeAuthoringState(facts);
  const override = WIDGET_WORDING[descriptor.state];

  const heading = override ? override.heading : descriptor.heading;
  const sentence = override ? override.sentence : descriptor.sentence;
  const nextAction = override ? override.nextAction : descriptor.nextAction;

  return (
    <div
      className={"widget-state widget-state--" + descriptor.tone}
      data-widget-state={descriptor.state}
      data-widget-tone={descriptor.tone}
      data-testid="widget-state"
      role={descriptor.tone === "danger" ? "alert" : "status"}
    >
      <strong className="widget-state__heading">{heading}</strong>
      {sentence ? <p className="widget-state__sentence">{sentence}</p> : null}
      {nextAction ? <p className="widget-state__next">{nextAction}</p> : null}
    </div>
  );
}

export default WidgetStatePanel;
