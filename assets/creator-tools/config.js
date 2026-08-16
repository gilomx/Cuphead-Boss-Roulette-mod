const ids = ["enabled", "boss", "weapon1", "weapon2", "super", "charm", "modifier"];
const el = Object.fromEntries(ids.map(id => [id, document.getElementById(id)]));
const statusEl = document.getElementById("status");
let state = null;
let catalogReady = false;
let sending = false;
let pending = null;

function setStatus(text, error = false) {
  statusEl.textContent = text;
  statusEl.dataset.error = error ? "true" : "false";
}

function fill(select, options, selected) {
  const value = String(selected);
  select.replaceChildren(...options.map(item => {
    const option = document.createElement("option");
    option.value = String(item.id);
    option.textContent = item.name;
    return option;
  }));
  if ([...select.options].some(option => option.value === value)) select.value = value;
}

function selectedBoss() {
  return state.bosses.find(item => String(item.id) === el.boss.value) || state.bosses[0];
}

function rebuildWeapon2(preferred) {
  const first = el.weapon1.value;
  const options = state.weapons.filter(item => item.empty || String(item.id) !== first);
  const fallback = options.find(item => item.empty);
  const selected = options.some(item => String(item.id) === String(preferred))
    ? preferred
    : fallback && fallback.id;
  fill(el.weapon2, options, selected);
  if (el.weapon2.value === first) {
    const empty = options.find(item => item.empty);
    if (empty) el.weapon2.value = String(empty.id);
  }
}

function rebuildModifiers(preferred) {
  const boss = selectedBoss();
  const kind = boss && boss.plane ? "plane" : "ground";
  const options = state.modifiers.filter(item => item.kind === "both" || item.kind === kind);
  const fallback = options.find(item => item.none);
  const selected = options.some(item => String(item.id) === String(preferred))
    ? preferred
    : fallback && fallback.id;
  fill(el.modifier, options, selected);
}

function matchesPending(next) {
  if (!pending || !next.selection) return true;
  return next.enabled === pending.enabled &&
    ["boss", "weapon1", "weapon2", "super", "charm", "modifier"]
      .every(key => String(next.selection[key]) === String(pending[key]));
}

function render(next, forceValues = false) {
  state = next;
  const selection = next.selection;
  if (!catalogReady) {
    fill(el.boss, next.bosses, selection.boss);
    fill(el.weapon1, next.weapons.filter(item => !item.empty), selection.weapon1);
    rebuildWeapon2(selection.weapon2);
    fill(el.super, next.supers, selection.super);
    fill(el.charm, next.charms, selection.charm);
    rebuildModifiers(selection.modifier);
    catalogReady = true;
  } else if (forceValues && !pending && document.activeElement.tagName !== "SELECT") {
    el.boss.value = String(selection.boss);
    el.weapon1.value = String(selection.weapon1);
    rebuildWeapon2(selection.weapon2);
    el.super.value = String(selection.super);
    el.charm.value = String(selection.charm);
    rebuildModifiers(selection.modifier);
  }
  if (pending && !matchesPending(next)) {
    setStatus("PENDIENTE: VUELVE A CUPHEAD");
    return;
  }
  if (pending) pending = null;
  el.enabled.checked = !!next.enabled;
  document.body.dataset.forced = next.enabled ? "true" : "false";
  setStatus(next.enabled ? "FORZADO ACTIVO EN CUPHEAD" : "FORZADO DESACTIVADO");
}

async function load(forceValues = false) {
  if (sending) return;
  try {
    const response = await fetch("/api/config", { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const next = await response.json();
    if (!next.ready) throw new Error("Cuphead todavía no publicó el catálogo");
    render(next, forceValues);
  } catch (error) {
    setStatus("SIN CONEXIÓN CON EL MOD", true);
  }
}

async function send() {
  if (!state) return;
  sending = true;
  pending = {
    enabled: el.enabled.checked,
    boss: el.boss.value,
    weapon1: el.weapon1.value,
    weapon2: el.weapon2.value,
    super: el.super.value,
    charm: el.charm.value,
    modifier: el.modifier.value
  };
  const params = new URLSearchParams({
    ...pending,
    enabled: pending.enabled ? "1" : "0"
  });
  try {
    const response = await fetch(`/api/config/set?${params}`, { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    setStatus("APLICANDO EN CUPHEAD…");
  } catch (error) {
    setStatus("NO SE PUDO APLICAR", true);
  } finally {
    sending = false;
    setTimeout(() => load(true), 160);
  }
}

el.boss.addEventListener("change", () => { rebuildModifiers(el.modifier.value); send(); });
el.weapon1.addEventListener("change", () => { rebuildWeapon2(el.weapon2.value); send(); });
el.weapon2.addEventListener("change", send);
el.super.addEventListener("change", send);
el.charm.addEventListener("change", send);
el.modifier.addEventListener("change", send);
el.enabled.addEventListener("change", send);

load(true);
setInterval(() => load(false), 900);
