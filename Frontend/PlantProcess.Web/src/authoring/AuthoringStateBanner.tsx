// PPIQ T-040. THE SEVEN STATES, RENDERED.
//
// One component for every state, because seven components would drift into
// seven different tones of voice. It renders what the model decided and adds
// nothing: no cause, no reassurance, no colour standing in for a sentence.
//
// The tone class carries the Golden Gate palette - Amber for blocked and
// refused, Hot Red for failed, Muted Steel for empty and filtered-empty - and
// the heading, sentence and next action carry the meaning. Remove the
// stylesheet entirely and every state still reads correctly, which is the test
// G17 actually sets.

import { describeAuthoringState, type AuthoringStateFacts } from "./authoringStates";
import "./authoring-states.css";

export interface AuthoringStateBannerProps {
  facts: AuthoringStateFacts;
}

export function AuthoringStateBanner({ facts }: AuthoringStateBannerProps) {
  const described = describeAuthoringState(facts);

  return (
    <div
      className={"ppiq-state ppiq-state--" + described.tone}
      data-testid="authoring-state"
      data-state={described.state}
      role={described.tone === "danger" ? "alert" : "status"}
    >
      <span className="ppiq-state__heading">{described.heading}</span>
      {described.sentence && (
        <p className="ppiq-state__sentence">{described.sentence}</p>
      )}
      {described.nextAction && (
        <p className="ppiq-state__action" data-testid="authoring-state-action">
          {described.nextAction}
        </p>
      )}
    </div>
  );
}

export default AuthoringStateBanner;