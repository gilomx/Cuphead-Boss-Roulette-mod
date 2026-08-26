import { useEffect, useMemo, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import type {
  StreamRule,
  StreamRuleDraft,
  TikTokGiftCatalog,
} from "../../model";
import { interactionItemFor, interactionItems } from "./interactionCatalog";

function draftFor(rule: StreamRule): StreamRuleDraft {
  return {
    id: rule.id,
    name: rule.name,
    enabled: rule.enabled,
    giftId: rule.giftId,
    every: rule.every,
    interaction: rule.interaction,
    quantity: rule.quantity,
  };
}

export function StreamRulesView() {
  const {
    streamRules,
    saveStreamRule,
    deleteStreamRule,
    duplicateStreamRule,
    toggleStreamRule,
  } = useConfig();
  const { t } = useLocalization();
  const [catalog, setCatalog] = useState<TikTokGiftCatalog | null>(null);
  const [catalogError, setCatalogError] = useState(false);
  const [draft, setDraft] = useState<StreamRuleDraft | null>(null);
  const [giftSearch, setGiftSearch] = useState("");

  useEffect(() => {
    let active = true;
    fetch("/assets/creator-tools/gifts/catalog.json", { cache: "no-store" })
      .then((response) => {
        if (!response.ok) throw new Error("HTTP " + response.status);
        return response.json() as Promise<TikTokGiftCatalog>;
      })
      .then((next) => {
        if (!active) return;
        setCatalog(next);
        setCatalogError(false);
      })
      .catch(() => {
        if (!active) return;
        setCatalogError(true);
      });
    return () => { active = false; };
  }, []);

  const gifts = useMemo(() => {
    const query = giftSearch.trim().toLocaleLowerCase();
    if (!catalog || query.length === 0) return catalog?.gifts ?? [];
    return catalog.gifts.filter((gift) =>
      gift.name.toLocaleLowerCase().includes(query) ||
      gift.giftId.includes(query) ||
      String(gift.coinsPerUnit).includes(query));
  }, [catalog, giftSearch]);

  const beginCreate = () => {
    const firstGift = catalog?.gifts[0];
    setDraft({
      name: firstGift?.name ?? "",
      enabled: true,
      giftId: firstGift?.giftId ?? "",
      every: 1,
      interaction: interactionItems[0].id,
      quantity: 1,
    });
    setGiftSearch("");
  };

  const beginEdit = (rule: StreamRule) => {
    setDraft(draftFor(rule));
    setGiftSearch("");
  };

  const rules = streamRules?.rules ?? [];
  const canSave = Boolean(
    streamRules?.ready && catalog && draft && draft.name.trim() &&
    draft.giftId && draft.interaction && draft.every >= 1 &&
    draft.quantity >= 1,
  );

  return (
    <div className="stream-rules">
      <section className="stream-rules__notice" data-error={catalogError || streamRules?.error}>
        <div>
          <span>{t("interactions.rules.notice.eyebrow")}</span>
          <strong>{t("interactions.rules.notice.title")}</strong>
          <p>{t("interactions.rules.notice.description")}</p>
        </div>
        <small>
          {catalog
            ? t("interactions.rules.notice.catalog", `${catalog.giftCount} regalos`)
                .replace("{count}", String(catalog.giftCount))
                .replace("{version}", catalog.catalogVersion)
            : t(catalogError
                ? "interactions.rules.notice.catalogError"
                : "interactions.rules.notice.catalogLoading")}
        </small>
      </section>

      <div className="stream-rules__workspace">
        <section className="interaction-panel stream-rules-list" aria-labelledby="stream-rules-list-title">
          <div className="interaction-panel__heading stream-rules-list__heading">
            <div>
              <h2 id="stream-rules-list-title">{t("interactions.rules.list.title")}</h2>
              <p>{t("interactions.rules.list.description")}</p>
            </div>
            <button type="button" onClick={beginCreate} disabled={!catalog || !streamRules?.ready}>
              {t("interactions.rules.list.create")}
            </button>
          </div>

          {rules.length === 0 ? (
            <div className="stream-rules-list__empty">
              <strong>{t("interactions.rules.list.emptyTitle")}</strong>
              <span>{t("interactions.rules.list.emptyDescription")}</span>
            </div>
          ) : (
            <div className="stream-rule-cards">
              {rules.map((rule) => {
                const gift = catalog?.gifts.find((entry) => entry.giftId === rule.giftId);
                const interaction = interactionItemFor(rule.interaction);
                return (
                  <article className="stream-rule-card" data-enabled={rule.enabled} key={rule.id}>
                    <div className="stream-rule-card__visual">
                      {gift ? <img src={gift.imagePath} alt="" /> : null}
                    </div>
                    <div className="stream-rule-card__copy">
                      <div className="stream-rule-card__title">
                        <strong>{rule.name}</strong>
                        <label className="stream-rule-toggle">
                          <span>{t(rule.enabled
                            ? "interactions.rules.list.enabled"
                            : "interactions.rules.list.disabled")}</span>
                          <input
                            type="checkbox"
                            checked={rule.enabled}
                            onChange={(event) => toggleStreamRule(rule.id, event.target.checked)}
                          />
                        </label>
                      </div>
                      <p>
                        {rule.giftName} · {rule.coinsPerUnit} {t("interactions.rules.coins")}
                        {rule.every > 1
                          ? ` · ${t("interactions.rules.list.every").replace("{count}", String(rule.every))}`
                          : ""}
                      </p>
                      <span>
                        {interaction ? t(interaction.titleKey) : rule.interaction}
                        {rule.quantity > 1 ? ` × ${rule.quantity}` : ""}
                      </span>
                    </div>
                    <div className="stream-rule-card__actions">
                      <button type="button" onClick={() => beginEdit(rule)}>
                        {t("interactions.rules.actions.edit")}
                      </button>
                      <button type="button" onClick={() => duplicateStreamRule(rule.id)}>
                        {t("interactions.rules.actions.duplicate")}
                      </button>
                      <button
                        type="button"
                        className="stream-rule-card__delete"
                        onClick={() => {
                          if (window.confirm(t("interactions.rules.actions.deleteConfirm"))) {
                            if (draft?.id === rule.id) setDraft(null);
                            deleteStreamRule(rule.id);
                          }
                        }}
                      >
                        {t("interactions.rules.actions.delete")}
                      </button>
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </section>

        <section className="interaction-panel stream-rule-editor" aria-labelledby="stream-rule-editor-title">
          <div className="interaction-panel__heading">
            <div>
              <h2 id="stream-rule-editor-title">
                {t(draft?.id === undefined
                  ? "interactions.rules.editor.createTitle"
                  : "interactions.rules.editor.editTitle")}
              </h2>
              <p>{t("interactions.rules.editor.description")}</p>
            </div>
          </div>

          {!draft ? (
            <div className="stream-rule-editor__empty">
              <strong>{t("interactions.rules.editor.emptyTitle")}</strong>
              <span>{t("interactions.rules.editor.emptyDescription")}</span>
              <button type="button" onClick={beginCreate} disabled={!catalog || !streamRules?.ready}>
                {t("interactions.rules.list.create")}
              </button>
            </div>
          ) : (
            <form
              className="stream-rule-form"
              onSubmit={(event) => {
                event.preventDefault();
                if (!canSave) return;
                saveStreamRule(draft);
                setDraft(null);
              }}
            >
              <label className="stream-rule-form__wide">
                <span>{t("interactions.rules.editor.name")}</span>
                <input
                  type="text"
                  maxLength={64}
                  value={draft.name}
                  onChange={(event) => setDraft({ ...draft, name: event.target.value })}
                />
              </label>

              <div className="stream-rule-fixed-fields stream-rule-form__wide">
                <div><span>{t("interactions.rules.editor.platform")}</span><strong>TikTok</strong></div>
                <div><span>{t("interactions.rules.editor.connection")}</span><strong>{t("interactions.rules.editor.allConnections")}</strong></div>
                <div><span>{t("interactions.rules.editor.event")}</span><strong>{t("interactions.rules.editor.gift")}</strong></div>
              </div>

              <fieldset className="stream-gift-picker stream-rule-form__wide">
                <legend>{t("interactions.rules.editor.selectGift")}</legend>
                <input
                  type="search"
                  value={giftSearch}
                  placeholder={t("interactions.rules.editor.searchGift")}
                  onChange={(event) => setGiftSearch(event.target.value)}
                />
                <div className="stream-gift-picker__grid">
                  {gifts.map((gift) => (
                    <button
                      type="button"
                      data-selected={gift.giftId === draft.giftId}
                      key={gift.giftId}
                      onClick={() => setDraft({
                        ...draft,
                        giftId: gift.giftId,
                        name: draft.name.trim() ? draft.name : gift.name,
                      })}
                    >
                      <img src={gift.imagePath} alt="" />
                      <span>{gift.name}</span>
                      <small>{gift.coinsPerUnit} {t("interactions.rules.coins")}</small>
                    </button>
                  ))}
                </div>
              </fieldset>

              <label>
                <span>{t("interactions.rules.editor.every")}</span>
                <input
                  type="number"
                  min={1}
                  max={streamRules?.maxEvery ?? 1000000}
                  value={draft.every}
                  onChange={(event) => setDraft({
                    ...draft,
                    every: Math.max(1, Number(event.target.value) || 1),
                  })}
                />
                <small>{t("interactions.rules.editor.everyHint")}</small>
              </label>

              <label>
                <span>{t("interactions.rules.editor.interaction")}</span>
                <select
                  value={draft.interaction}
                  onChange={(event) => setDraft({ ...draft, interaction: event.target.value })}
                >
                  {interactionItems.map((item) => (
                    <option value={item.id} key={item.id}>{t(item.titleKey)}</option>
                  ))}
                </select>
              </label>

              <label>
                <span>{t("interactions.rules.editor.quantity")}</span>
                <input
                  type="number"
                  min={1}
                  max={streamRules?.maxQuantity ?? 50}
                  value={draft.quantity}
                  onChange={(event) => setDraft({
                    ...draft,
                    quantity: Math.max(1, Number(event.target.value) || 1),
                  })}
                />
              </label>

              <label className="stream-rule-form__enabled">
                <input
                  type="checkbox"
                  checked={draft.enabled}
                  onChange={(event) => setDraft({ ...draft, enabled: event.target.checked })}
                />
                <span>{t("interactions.rules.editor.enabled")}</span>
              </label>

              <div className="stream-rule-form__actions stream-rule-form__wide">
                <button type="button" onClick={() => setDraft(null)}>
                  {t("interactions.rules.actions.cancel")}
                </button>
                <button type="submit" disabled={!canSave}>
                  {t("interactions.rules.actions.save")}
                </button>
              </div>
            </form>
          )}

          <p className="stream-rule-feedback" data-error={streamRules?.error ?? false} role="status">
            {t(`interactions.rules.feedback.${streamRules?.feedback ?? "ready"}`)}
          </p>
        </section>
      </div>
    </div>
  );
}
