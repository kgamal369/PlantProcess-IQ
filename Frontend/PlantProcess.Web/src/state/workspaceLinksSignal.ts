/// PPIQ T-042 S6. ONE NAME, ONE PLACE.
///
/// Navigation is built once when AppLayout mounts, and AppLayout stays mounted
/// while an author publishes a page. Without an explicit signal a successful
/// Publish would be invisible until a browser reload, which is not the contract.
///
/// This is INVALIDATION ONLY. It carries no page data, keeps no cache and owns
/// no state: it says "the set of visible workspaces may have changed, ask the
/// server again". Anything more would be a second copy of data that already has
/// one authority.
export const WORKSPACE_LINKS_CHANGED = "ppiq:workspaces-changed";

/// Fired only AFTER a server mutation has been confirmed. Firing on intent
/// rather than on confirmation would show a workspace the server never
/// published.
export function notifyWorkspaceLinksChanged(): void {
  if (typeof window === "undefined") {
    return;
  }

  window.dispatchEvent(new Event(WORKSPACE_LINKS_CHANGED));
}

export function subscribeToWorkspaceLinksChanged(listener: () => void): () => void {
  if (typeof window === "undefined") {
    return () => undefined;
  }

  window.addEventListener(WORKSPACE_LINKS_CHANGED, listener);

  return () => window.removeEventListener(WORKSPACE_LINKS_CHANGED, listener);
}