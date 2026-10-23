export function matchesOverlayPreviewLayout(layout, profile) {
  if (!layout || !profile || layout.id !== profile.id ||
      !Array.isArray(layout.components) || !Array.isArray(profile.components) ||
      layout.components.length !== profile.components.length) return false;
  const expectedIds = new Set(profile.components.map(component => component.id));
  const receivedIds = new Set(layout.components.map(component => component?.id));
  return receivedIds.size === expectedIds.size &&
    [...receivedIds].every(id => expectedIds.has(id));
}
