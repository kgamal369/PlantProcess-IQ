# PPIQ Hostile-Hands Protocol - M1-03

**The customer takes the mouse.**

*25 July 2026 | Execute before the demonstration | This document survives the demo and becomes the template for the post-demo audit*

---

## Why this exists, and how it differs from every other check

The consolidated test pass walks **your** path and proves **your** path.

This protocol does something else. You execute it **as the sceptical plant engineer sitting across the table** - the one who takes the mouse, clicks where you did not plan, and is looking for the seam. He is not trying to see the product work. He is trying to find out whether it is real.

Three lenses, per the audit concept:

| Lens | The question | Who answers it |
|---|---|---|
| **1 Existence and wiring** | Does the control exist and is it wired to anything? | The M1-02 script |
| **2 Presentation** | Does it look like a product a large company pays thousands a month for? | **You, here** |
| **3 Deep wiring** | Is the wiring *correct*? Is the dropdown fed from live data? Does the save reach the right place? | **You, here** |

The script closes lens one. **This document is lenses two and three, and only a human can run it.**

---

## How to grade

Every line gets exactly one mark. No half marks, per the no-partial-credit rule.

| Mark | Meaning | What happens next |
|---|---|---|
| **P** | Behaves correctly and looks professional | Nothing |
| **F** | Fails, on the demo path | Becomes M1-07 scope. Must be fixed or the surface leaves the demo |
| **F-off** | Fails, off the demo path | Remove it from the navigation for the demo, or accept it silently |
| **C** | Cut deliberately | Enters the M1-06 cut register with its spoken sentence |

**Rule for the whole protocol:** if you find yourself thinking *"he probably won't click that"* - that is the exact control to test. He will.

---

# PART 1 - THE AUTHORING CANVAS

*This is your strongest surface and the one you now open the authoring story with. It is also where the engineer will push hardest, because it is the part that looks like real software.*

| # | Do this | It must | Mark |
|---|---|---|---|
| 1.1 | Wire two blocks that must not connect. A value output into a dataset input. | **Refuse the wire AND write a sentence saying why.** Not a silent no-op. Not a red outline with no words. | |
| 1.2 | Wire a node's output back into its own input, making a loop | Refuse, and name the cycle | |
| 1.3 | Leave a required input unconnected and press Run | Refuse before running, naming which input | |
| 1.4 | Drag two tables onto the board and compare a column of one with a column of the other, with no join between them | Refuse and say a join is required. **This is the mistake a plant engineer actually makes** | |
| 1.5 | Unfold the left tree three levels: schema, table, column | Every level unfolds. Column types are visible | |
| 1.6 | Check what schemas the tree lists | **No emulated-source schema is visible.** If the customer sees a schema named after a source emulator, Rule 1 breaks in front of him | |
| 1.7 | Drag one column. Then drag a whole table | Both work | |
| 1.8 | Delete an edge with the keyboard. Duplicate a node | Both work, or both are absent - not one working and one silently doing nothing | |
| 1.9 | Zoom out fully, then fit to view | The graph stays legible and the minimap tracks | |
| 1.10 | Publish, then reopen the saved definition | It returns with its graph intact and a version number | |
| 1.11 | Look for a SQL toggle | **There is none.** If asked, the sentence from M1-06 covers it. Do not go looking during the demo | |

---

# PART 2 - THE WORKSPACE AND ITS WIDGETS

*The surrounding-environment lens. Every one of these failed somewhere in an earlier session.*

| # | Do this | It must | Mark |
|---|---|---|---|
| 2.1 | Press **maximise** on every widget on the page. Then minimise each one | Every one responds. No widget where the control is present and does nothing | |
| 2.2 | Resize a widget by its edge, then by its corner | Both work. The chart re-renders to fit rather than clipping or overflowing | |
| 2.3 | Resize the **donut** specifically, then look at it closely | It does not collapse to a tiny square. This was measured at 14 by 14 pixels once and never re-checked | |
| 2.4 | Drag a widget to a different grid position. Reload the page | It is still where you put it | |
| 2.5 | Click a **pie slice** | The whole page cross-filters. Not just that widget | |
| 2.6 | Click a **bar segment** | Same | |
| 2.7 | Click a **heatmap cell** | Same, and the associative panel re-shades | |
| 2.8 | Click the same element again to deselect | The selection clears cleanly | |
| 2.9 | Look at the widget alignment across the whole page | Consistent gutters. No widget half a grid cell out of line | |
| 2.10 | Count widgets returning no data | **Zero.** M1-05 closes this. A page half empty reads as a broken product | |
| 2.11 | Switch a chart's type using the switcher | It re-renders. Incompatible types are absent from the switcher, not present and broken | |
| 2.12 | Look for an **Add widget** control | **There is none.** Do not hunt for it in the room. The M1-06 sentence covers it | |
| 2.13 | Press Edit on a widget card | Know what it does before the demo. It shares a handler with Rename, so it renames. If the customer expects a data editor, the M1-06 sentence covers it | |

---

# PART 3 - DROPDOWNS AND FORMS

*The deep-wiring lens. A dropdown that exists and is populated from a hardcoded array is the exact "looks fine, deeper look says wrong" case.*

| # | Do this | It must | Mark |
|---|---|---|---|
| 3.1 | Open **every** dropdown on the connections form | Every one has options | |
| 3.2 | For each, ask: did these come from live data or from a constant in the code? | Provider list may legitimately be fixed. **A table list, a column list or a schema list must be live** | |
| 3.3 | Pick a provider, then look at the fields below | The form adapts to the provider. Oracle and MySQL do not ask identical questions | |
| 3.4 | Type into every text field and check alignment | Text sits inside the field. Labels align with inputs. No field half a line off | |
| 3.5 | Press **Test connection** with correct details | A clear success state | |
| 3.6 | Press **Test connection** with a deliberately wrong password | A clear, specific failure. Not a generic red toast and not a silent nothing | |
| 3.7 | Save a connection, reopen it | The credential is masked, not blank and not in clear text | |
| 3.8 | Change the sync cadence dropdown and save | The change persists after reload | |
| 3.9 | Submit a form with a required field empty | It refuses and says which field | |
| 3.10 | On any filter bar, open a dropdown after selecting something elsewhere | Impossible values are visibly de-emphasised, not silently still selectable as if nothing happened | |

---

# PART 4 - CHANGING WHAT A CHART SHOWS

*The engineer will ask this. It is the most natural question a configuration-minded buyer has.*

| # | Do this | It must | Mark |
|---|---|---|---|
| 4.1 | Take a donut grouped by one dimension. Try to regroup it by a different one | Know the answer **before** the room. If the dimension is not registered on the server it returns 400 | |
| 4.2 | If it works, reload the page | The change persisted | |
| 4.3 | If it returns an error, look at what the customer sees | A described message, not a raw status code or a bare toast | |
| 4.4 | Try a measure the chart type cannot render | The safety registry refuses it cleanly, which is a **good** answer to show | |

**Decide 4.1 in advance.** If regrouping does not work, do not offer it, and have the sentence ready.

---

# PART 5 - THE ASSISTANT

*The specific fear: a question about speed answered with a weight. An answer outside the sane range for the question destroys the credibility of the whole intelligence claim in one sentence.*

| # | Do this | It must | Mark |
|---|---|---|---|
| 5.1 | Ask a **grounded** question: which source systems are registered | A cited answer | |
| 5.2 | Press **Enter** rather than clicking Ask | It sends | |
| 5.3 | Ask a question with **no** grounded evidence, for example the best speed to run for best quality | **The answer must be within the logical boundary of the question.** A speed question answered in kilograms is a demo-ending failure. An honest "I do not have evidence for that" is a **pass** | |
| 5.4 | Ask a question about a defect class that does not exist in the data | It should abstain rather than invent | |
| 5.5 | Click a citation | It expands a provenance handle. **It does not open a source row.** Know this and do not promise otherwise | |
| 5.6 | Stop the API. Ask again | **Red "Request failed", not amber "Insufficient evidence".** A transport fault must never be dressed as an evidential abstention | |
| 5.7 | Restart the API. Ask again | It recovers without a page reload | |

**If 5.3 produces nonsense, cut the free-question beat.** Ask only the two prepared grounded questions and say the assistant is trained on the imported evidence and improves with a larger dataset. That sentence is true and it is safe.

---

# PART 6 - EVERY PAGE IN THE NAVIGATION

*The engineer clicks the sidebar. All of it. Not just your six pages.*

| # | Do this | It must | Mark |
|---|---|---|---|
| 6.1 | Open **every** entry in the left navigation, one by one | Each loads. None shows a raw error or a blank white area | |
| 6.2 | On each, count the controls that do nothing | Cross-check against the M1-02 CSV for that route | |
| 6.3 | Count the navigation entries | If there are thirty flat items, that is a lens-two failure. Group and fold them, or reduce the demo navigation | |
| 6.4 | On each page, check the header, spacing and table styling against the workspace | One product, not several. A page with a different table style is visible immediately | |
| 6.5 | Find any page that is empty, stubbed or clearly unfinished | **Remove it from the navigation for the demo.** A hidden page costs nothing; a discovered stub costs the room | |
| 6.6 | Press the browser Back button after navigating three pages deep | It returns correctly without breaking state | |
| 6.7 | Reload the page mid-session on three different routes | Each recovers; you are not thrown to a blank screen | |

---

# PART 7 - INDUCED FAULTS

*Never read a claim about failure handling. Induce the condition and watch.*

| # | Do this | It must | Mark |
|---|---|---|---|
| 7.1 | Stop one emulated source container. Open the connections page | An honest error naming that source. The rest of the page still works | |
| 7.2 | Stop the API entirely. Click through three pages | Contained, branded, retryable errors. **Not a white screen** | |
| 7.3 | Restart the API. Retry | Recovery without a reload | |
| 7.4 | Force one widget's endpoint to fail while the others succeed | **Only that widget shows an error.** The page stays interactive. This is the single most important resilience behaviour a buyer will notice | |
| 7.5 | Run an analysis on an outcome you know is blocked | The readiness gate refuses with named dimensions and real numbers. **This is a beat, not a failure** | |
| 7.6 | Leave the browser idle for fifteen minutes, then click something | Either it still works, or it recovers cleanly. Not a silent 401 with a dead page | |

---

# PART 8 - THE SECOND PAIR OF EYES

*Lens two cannot be self-assessed reliably. You have looked at this product for months.*

Open the demo path and ask, on every screen:

1. Is anything **misaligned**? Two controls that should share a baseline and do not.
2. Is any list **long and flat** where it should be grouped and foldable?
3. Are the **table styles** identical across every page?
4. Is anything showing a **raw machine value** - an ISO timestamp, a UUID, an enum name, a status code?
5. Is any **empty state** blank rather than designed?
6. Does any **loading state** spin without progress?
7. Is any **colour** off the token set?
8. Does any label read like a **developer wrote it for himself**?

Every yes is a lens-two finding. They are individually small and collectively decisive, because they are what "professional" actually means to a buyer.

---

# CLOSING THE PROTOCOL

**M1-03 is complete when:**

- Every line above carries exactly one mark
- Every **F** on the demo path has become a task in M1-07, or its surface has left the demo
- Every **F-off** is either hidden from the navigation or consciously accepted
- Every **C** is in the M1-06 cut register with its spoken sentence
- The marked copy of this document is saved with the date

**Then re-run the M1-02 script.** Dead controls on the demo path must read zero.

---

## The one thing to carry into the room

You cannot make the product perfect in two days. You **can** know exactly where every seam is.

An engineer who finds a gap you already named, and hears you answer it in one calm prepared sentence, concludes the product is honest.

An engineer who finds a gap that visibly surprises you concludes nobody checked.

**The difference between those two rooms is this document.**
