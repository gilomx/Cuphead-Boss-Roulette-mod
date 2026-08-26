import { useCallback, useEffect, useRef, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useTikTokGiftCatalog } from "../../hooks/useTikTokGiftCatalog";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRule, StreamRuleDraft } from "../../model";
import { createStreamRuleDraft, draftForStreamRule } from "./streamRuleDraft";
import { StreamRuleForm } from "./StreamRuleForm";
import { StreamRulesTable } from "./StreamRulesTable";

type HighlightFeedback = "created" | "updated" | "duplicated";

interface HighlightRequest {
  feedback: HighlightFeedback;
  id?: number;
  previousIds: number[];
  revision: number;
  closeEditor: boolean;
}

export function StreamRulesView() {
  const {
    streamRules,
    saveStreamRule,
    deleteStreamRule,
    duplicateStreamRule,
    toggleStreamRule,
    status,
  } = useConfig();
  const { t } = useLocalization();
  const { catalog, error: catalogError } = useTikTokGiftCatalog();
  const [draft, setDraft] = useState<StreamRuleDraft | null>(null);
  const [savePending, setSavePending] = useState(false);
  const [highlightedRuleId, setHighlightedRuleId] = useState<number | null>(null);
  const highlightRequestRef = useRef<HighlightRequest | null>(null);
  const highlightTimerRef = useRef<number | null>(null);
  const rules = streamRules?.rules ?? [];
  const canCreate = Boolean(
    catalog && streamRules?.ready && rules.length < (streamRules?.maxRules ?? 0),
  );

  const highlightRule = useCallback((id: number) => {
    if (highlightTimerRef.current !== null) {
      window.clearTimeout(highlightTimerRef.current);
    }
    setHighlightedRuleId(id);
    highlightTimerRef.current = window.setTimeout(() => {
      setHighlightedRuleId(null);
      highlightTimerRef.current = null;
    }, 2400);
  }, []);

  useEffect(() => () => {
    if (highlightTimerRef.current !== null) {
      window.clearTimeout(highlightTimerRef.current);
    }
  }, []);

  useEffect(() => {
    const request = highlightRequestRef.current;
    if (!request || !streamRules || streamRules.revision <= request.revision) return;

    highlightRequestRef.current = null;
    if (request.closeEditor) setSavePending(false);
    if (streamRules.error || streamRules.feedback !== request.feedback) return;

    const id = request.id ?? streamRules.rules.find(
      (rule) => !request.previousIds.includes(rule.id),
    )?.id;
    if (request.closeEditor) setDraft(null);
    if (id !== undefined) highlightRule(id);
  }, [highlightRule, streamRules]);

  useEffect(() => {
    if (!savePending || status !== "error") return;
    highlightRequestRef.current = null;
    setSavePending(false);
  }, [savePending, status]);

  const requestHighlight = (
    feedback: HighlightFeedback,
    id?: number,
    closeEditor = false,
  ) => {
    highlightRequestRef.current = {
      feedback,
      id,
      previousIds: rules.map((rule) => rule.id),
      revision: streamRules?.revision ?? -1,
      closeEditor,
    };
  };

  const beginCreate = () => {
    if (!canCreate) return;
    setDraft(createStreamRuleDraft(catalog?.gifts[0]));
  };

  const beginEdit = (rule: StreamRule) => {
    setDraft(draftForStreamRule(rule));
  };

  const saveDraft = (nextDraft: StreamRuleDraft) => {
    requestHighlight(
      nextDraft.id === undefined ? "created" : "updated",
      nextDraft.id,
      true,
    );
    setSavePending(true);
    if (!saveStreamRule(nextDraft)) {
      highlightRequestRef.current = null;
      setSavePending(false);
    }
  };

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
            ? t("interactions.rules.notice.catalog", `${catalog.giftCount} gifts`)
                .replace("{count}", String(catalog.giftCount))
                .replace("{version}", catalog.catalogVersion)
            : t(catalogError
                ? "interactions.rules.notice.catalogError"
                : "interactions.rules.notice.catalogLoading")}
        </small>
      </section>

      <section
        className="interaction-panel stream-rules-panel"
        data-view={draft ? "editor" : "table"}
        aria-labelledby="stream-rules-panel-title"
      >
        <div className="interaction-panel__heading stream-rules-panel__heading">
          <div>
            <span className="stream-rules-panel__eyebrow">
              {t(draft
                ? "interactions.rules.editor.eyebrow"
                : "interactions.rules.list.eyebrow")}
            </span>
            <h2 id="stream-rules-panel-title">
              {t(draft
                ? draft.id === undefined
                  ? "interactions.rules.editor.createTitle"
                  : "interactions.rules.editor.editTitle"
                : "interactions.rules.list.title")}
            </h2>
            <p>{t(draft
              ? "interactions.rules.editor.description"
              : "interactions.rules.list.description")}</p>
          </div>

          {draft ? (
            <button
              type="button"
              className="stream-rule-back"
              disabled={savePending}
              onClick={() => setDraft(null)}
            >
              <span aria-hidden="true">&larr;</span>
              {t("interactions.rules.actions.back")}
            </button>
          ) : (
            <div className="stream-rules-panel__tools">
              <span
                className="interaction-count"
                aria-label={t("interactions.rules.list.countLabel")}
              >
                {rules.length}
              </span>
              <button
                type="button"
                className="stream-rule-create"
                onClick={beginCreate}
                disabled={!canCreate}
                title={streamRules?.ready && rules.length >= streamRules.maxRules
                  ? t("interactions.rules.list.limitReached")
                  : undefined}
              >
                <span aria-hidden="true">+</span>
                {t("interactions.rules.list.create")}
              </button>
            </div>
          )}
        </div>

        <div
          className="stream-rules-panel__view"
          data-direction={draft ? "forward" : "back"}
          key={draft ? `editor-${draft.id ?? "new"}` : "table"}
        >
          {draft && catalog ? (
            <StreamRuleForm
              draft={draft}
              gifts={catalog.gifts}
              maxEvery={streamRules?.maxEvery ?? 1_000_000}
              maxQuantity={streamRules?.maxQuantity ?? 50}
              saving={savePending}
              onChange={setDraft}
              onCancel={() => setDraft(null)}
              onSave={saveDraft}
            />
          ) : (
            <StreamRulesTable
              rules={rules}
              gifts={catalog?.gifts ?? []}
              canCreate={canCreate}
              disabled={!streamRules?.ready || !catalog}
              highlightedRuleId={highlightedRuleId}
              onCreate={beginCreate}
              onEdit={beginEdit}
              onToggle={toggleStreamRule}
              onDuplicate={(id) => {
                requestHighlight("duplicated");
                duplicateStreamRule(id);
              }}
              onDelete={deleteStreamRule}
            />
          )}
        </div>

        <p className="stream-rule-feedback" data-error={streamRules?.error ?? false} role="status" aria-live="polite">
          {t(`interactions.rules.feedback.${streamRules?.feedback ?? "ready"}`)}
        </p>
      </section>
    </div>
  );
}
