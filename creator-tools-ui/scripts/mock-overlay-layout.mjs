export function validInteractionPaymentSize(component) {
  if (!Object.hasOwn(component, "interactionsPaymentSize")) return true;
  const size = component.interactionsPaymentSize;
  return component.id === "interactions" && Number.isInteger(size) && size >= 100 && size <= 200;
}

export function matchesOverlayPreviewLayout(layout, profile) {
  if (!layout || !profile || layout.id !== profile.id ||
      !Array.isArray(layout.components) || !Array.isArray(profile.components) ||
      layout.components.length !== profile.components.length) return false;
  const expectedIds = new Set(profile.components.map(component => component.id));
  const receivedIds = new Set(layout.components.map(component => component?.id));
  return receivedIds.size === expectedIds.size &&
    [...receivedIds].every(id => expectedIds.has(id)) && layout.components.every(validInteractionPaymentSize);
}
