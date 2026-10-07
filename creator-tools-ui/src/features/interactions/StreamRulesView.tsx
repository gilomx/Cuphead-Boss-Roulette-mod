import { useCallback, useEffect, useRef, useState } from "react";
import { ArrowLeft, Plus, Settings } from "lucide-react";
import { useConfig } from "../../config/ConfigContext";
import { useTikTokGiftCatalog } from "../../hooks/useTikTokGiftCatalog";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRule, StreamRuleDraft, StreamRulePlatform } from "../../model";
import { StreamPlatformIcon } from "./StreamRuleIcons";
import {
  createStreamRuleDraft,
  draftForStreamRule,
  sameStreamRuleDraft,
} from "./streamRuleDraft";
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

interface VisibleRuleFeedback {
  key: string;
  error: boolean;
}

interface StreamRulesViewProps {
  onOpenSettings: () => void;
  onOpenAdvancedSettings: () => void;
  testSentNotice: boolean;
  onTestSentNoticeDismissed: () => void;
}

const silentRuleFeedback = new Set(["created", "deleted", "enabled", "disabled"]);

export function StreamRulesView({
  onOpenSettings,
  onOpenAdvancedSettings,
  testSentNotice,
  onTestSentNoticeDismissed,
}: StreamRulesViewProps) {
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
  const [platformFilter, setPlatformFilter] = useState<StreamRulePlatform | "all">("all");
  const [savePending, setSavePending] = useState(false);
  const [highlightedRuleId, setHighlightedRuleId] = useState<number | null>(null);
  const [confirmingDeleteId, setConfirmingDeleteId] = useState<number | null>(null);
  const [deletingRuleId, setDeletingRuleId] = useState<number | null>(null);
  const [visibleRuleFeedback, setVisibleRuleFeedback] = useState<
    VisibleRuleFeedback | null
  >(null);
  const highlightRequestRef = useRef<HighlightRequest | null>(null);
  const highlightTimerRef = useRef<number | null>(null);
  const scrollFrameRef = useRef<number | null>(null);
  const feedbackTimerRef = useRef<number | null>(null);
  const panelRef = useRef<HTMLElement>(null);
  const observedRulesStateRef = useRef(false);
  const initialDraftRef = useRef<StreamRuleDraft | null>(null);
  const editorOpen = draft !== null;
  const draftChanged = editorOpen &&
    !sameStreamRuleDraft(draft, initialDraftRef.current);
  const rules = streamRules?.rules ?? [];
  const canCreate = Boolean(
    catalog && streamRules?.ready && rules.length < (streamRules?.maxRules ?? 0),
  );
  const feedbackKey = catalogError
    ? "interactions.rules.notice.catalogError"
    : testSentNotice
        ? "interactions.test.sentNotice"
        : !catalog
          ? "interactions.rules.notice.catalogLoading"
          : visibleRuleFeedback?.key ?? null;
  const feedbackError = Boolean(catalogError || (!testSentNotice && visibleRuleFeedback?.error));

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
    if (feedbackTimerRef.current !== null) {
      window.clearTimeout(feedbackTimerRef.current);
    }
    if (scrollFrameRef.current !== null) {
      window.cancelAnimationFrame(scrollFrameRef.current);
    }
  }, []);

  useEffect(() => {
    if (!editorOpen) return;
    const returnOnEscape = (event: globalThis.KeyboardEvent) => {
      if (event.key !== "Escape" || event.defaultPrevented ||
          event.isComposing || savePending || draftChanged) return;
      event.preventDefault();
      setDraft(null);
    };
    window.addEventListener("keydown", returnOnEscape);
    return () => window.removeEventListener("keydown", returnOnEscape);
  }, [draftChanged, editorOpen, savePending]);

  useEffect(() => {
    if (!testSentNotice) return;
    const timer = window.setTimeout(onTestSentNoticeDismissed, 5200);
    return () => window.clearTimeout(timer);
  }, [onTestSentNoticeDismissed, testSentNotice]);

  useEffect(() => {
    if (draft !== null || highlightedRuleId === null) return;
    if (scrollFrameRef.current !== null) {
      window.cancelAnimationFrame(scrollFrameRef.current);
    }
    scrollFrameRef.current = window.requestAnimationFrame(() => {
      scrollFrameRef.current = null;
      const row = panelRef.current?.querySelector<HTMLElement>(
        `.stream-rule-row[data-rule-id="${highlightedRuleId}"]`,
      );
      if (!row) return;
      row.scrollIntoView({
        behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches
          ? "auto"
          : "smooth",
        block: "center",
      });
    });
  }, [draft, highlightedRuleId]);

  useEffect(() => {
    if (!streamRules) return;
    const feedback = streamRules.feedback;
    if (!observedRulesStateRef.current) {
      observedRulesStateRef.current = true;
      if (streamRules.error && feedback && feedback !== "ready") {
        setVisibleRuleFeedback({
          key: `interactions.rules.feedback.${feedback}`,
          error: true,
        });
      }
      return;
    }

    if (!feedback || feedback === "ready") {
      setVisibleRuleFeedback(null);
      return;
    }
    if (!streamRules.error && silentRuleFeedback.has(feedback)) {
      setVisibleRuleFeedback(null);
      return;
    }
    if (feedbackTimerRef.current !== null) {
      window.clearTimeout(feedbackTimerRef.current);
      feedbackTimerRef.current = null;
    }
    setVisibleRuleFeedback({
      key: `interactions.rules.feedback.${feedback}`,
      error: streamRules.error,
    });
    if (!streamRules.error) {
      feedbackTimerRef.current = window.setTimeout(() => {
        setVisibleRuleFeedback(null);
        feedbackTimerRef.current = null;
      }, 3200);
    }
  }, [streamRules?.error, streamRules?.feedback, streamRules?.revision]);

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
    if (id !== undefined) {
      const saved = streamRules.rules.find((rule) => rule.id === id);
      if (saved && platformFilter !== "all" && saved.platform !== platformFilter)
        setPlatformFilter(saved.platform);
      highlightRule(id);
    }
  }, [highlightRule, platformFilter, streamRules]);

  useEffect(() => {
    if (!savePending || status !== "error") return;
    highlightRequestRef.current = null;
    setSavePending(false);
  }, [savePending, status]);

  useEffect(() => {
    if (
      confirmingDeleteId !== null &&
      !rules.some((rule) => rule.id === confirmingDeleteId)
    ) {
      setConfirmingDeleteId(null);
    }
    if (
      deletingRuleId !== null &&
      !rules.some((rule) => rule.id === deletingRuleId)
    ) {
      setDeletingRuleId(null);
    }
  }, [confirmingDeleteId, deletingRuleId, rules]);

  useEffect(() => {
    if (deletingRuleId !== null && status === "error") {
      setDeletingRuleId(null);
    }
  }, [deletingRuleId, status]);

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
    const nextDraft = createStreamRuleDraft(
      catalog?.gifts[0], platformFilter === "twitch" ? "twitch" : "tiktok",
    );
    initialDraftRef.current = nextDraft;
    setDraft(nextDraft);
  };

  const beginEdit = (rule: StreamRule) => {
    const nextDraft = draftForStreamRule(rule);
    initialDraftRef.current = nextDraft;
    setDraft(nextDraft);
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
      <section
        ref={panelRef}
        className="interaction-panel stream-rules-panel"
        data-view={draft ? "editor" : "table"}
        aria-labelledby="stream-rules-panel-title"
      >
        <div className="interaction-panel__heading stream-rules-panel__heading">
          <div>
            {draft ? (
              <span className="stream-rules-panel__eyebrow">
                {t("interactions.rules.editor.eyebrow")}
              </span>
            ) : null}
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
              className="stream-rule-back stream-rule-back--icon"
              disabled={savePending}
              onClick={() => setDraft(null)}
              aria-label={t("interactions.rules.actions.back")}
              title={t("interactions.rules.actions.back")}
            >
              <ArrowLeft aria-hidden="true" />
            </button>
          ) : (
            <div className="stream-rules-panel__tools">
              <button
                type="button"
                className="stream-rule-tool stream-rule-settings"
                onClick={onOpenSettings}
                aria-label={t("interactions.settings.title")}
                title={t("interactions.settings.title")}
              >
                <Settings aria-hidden="true" />
              </button>
              <button
                type="button"
                className="stream-rule-create stream-rule-create--icon"
                onClick={beginCreate}
                disabled={!canCreate}
                aria-label={t("interactions.rules.list.create")}
                title={streamRules?.ready && rules.length >= streamRules.maxRules
                  ? t("interactions.rules.list.limitReached")
                  : t("interactions.rules.list.create")}
              >
                <Plus aria-hidden="true" />
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
              maxCooldownSeconds={streamRules?.maxCooldownSeconds ?? 3_600}
              saving={savePending}
              onOpenAdvancedSettings={onOpenAdvancedSettings}
              onChange={setDraft}
              onCancel={() => setDraft(null)}
              onSave={saveDraft}
            />
          ) : (
            <>
            <div className="stream-platform-filters" role="group" aria-label={t("interactions.rules.list.platformFilter")}>
              {(["all", "tiktok", "twitch"] as const).map((platform) => (
                <button type="button" key={platform} aria-pressed={platformFilter === platform}
                  aria-label={platform === "all" ? t("interactions.rules.list.allPlatforms") : platform === "tiktok" ? "TikTok" : "Twitch"}
                  onClick={() => { setPlatformFilter(platform); setConfirmingDeleteId(null); }}>
                  {platform !== "all" ? <StreamPlatformIcon platform={platform} /> : null}
                  {platform === "all" ? t("interactions.rules.list.allPlatforms") : platform === "tiktok" ? "TikTok" : "Twitch"}
                </button>
              ))}
            </div>
            <StreamRulesTable
              rules={rules.filter((rule) => platformFilter === "all" || rule.platform === platformFilter)}
              gifts={catalog?.gifts ?? []}
              canCreate={canCreate}
              disabled={!streamRules?.ready || !catalog || deletingRuleId !== null}
              highlightedRuleId={highlightedRuleId}
              confirmingDeleteId={confirmingDeleteId}
              deletingRuleId={deletingRuleId}
              onCreate={beginCreate}
              onEdit={beginEdit}
              onToggle={toggleStreamRule}
              onDuplicate={(id) => {
                requestHighlight("duplicated");
                duplicateStreamRule(id);
              }}
              onRequestDelete={setConfirmingDeleteId}
              onCancelDelete={() => setConfirmingDeleteId(null)}
              onConfirmDelete={(id) => {
                if (confirmingDeleteId !== id || deletingRuleId !== null) return;
                if (deleteStreamRule(id)) setDeletingRuleId(id);
              }}
            />
            </>
          )}
        </div>

        {feedbackKey ? (
          <p
            className="stream-rule-feedback"
            data-error={feedbackError}
            role="status"
            aria-live="polite"
          >
            {t(feedbackKey)}
          </p>
        ) : null}
      </section>
    </div>
  );
}
