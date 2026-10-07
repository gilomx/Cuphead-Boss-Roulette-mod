import { ArrowLeft, Check, MessageCircleCheck } from "lucide-react";
import { useEffect, useRef, useState, type CSSProperties } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { ChatChoosesStage } from "../../model";
import "../../styles/chat-chooses.css";
import { OverlayDesignerCallout } from "./OverlayDesignerCallout";

const STAGES: ChatChoosesStage[] = ["boss", "weapon1", "weapon2", "super", "charm", "modifier"];

export function ChatChoosesDetailView({ onBack, onOpenOverlayDesigner }: { onBack: () => void; onOpenOverlayDesigner: () => void }) {
  const { chatChooses: state, sendChatChooses, liveEvents } = useConfig();
  const { locale, t } = useLocalization();
  const [busy, setBusy] = useState(false);
  const [requestError, setRequestError] = useState(false);
  const backRef = useRef<HTMLButtonElement>(null);
  const phase = state?.phase ?? "off";
  const locked = phase !== "off" && phase !== "completed";
  const blocked = Boolean(liveEvents?.activeEvent && liveEvents.activeEvent !== "chat_chooses");
  const activeStages = STAGES.filter((stage) => (!state?.plane || !["weapon1", "weapon2", "super"].includes(stage)) &&
    (stage !== "modifier" || state?.withChallenge));
  const votesLabel = (count: number) => t(`dashboard.chatChooses.${count === 1 ? "vote" : "votes"}`).replace("{count}", String(count));
  const highestVotes = Math.max(0, ...(state?.options ?? []).map(option => option.votes ?? 0));
  const voteCount = (count: number) => count < 1000 ? String(count) :
    new Intl.NumberFormat(locale, { notation: "compact", maximumFractionDigits: 1 }).format(count);

  useEffect(() => { backRef.current?.focus({ preventScroll: true }); }, []);
  useEffect(() => {
    const keyDown = (event: KeyboardEvent) => { if (event.key === "Escape") onBack(); };
    window.addEventListener("keydown", keyDown);
    return () => window.removeEventListener("keydown", keyDown);
  }, [onBack]);

  const command = async (operation: "save" | "start" | "next" | "stop" | "finish" | "test_votes", withChallenge?: boolean) => {
    if (busy) return;
    setBusy(true); setRequestError(false);
    try { await sendChatChooses(operation, withChallenge); }
    catch { setRequestError(true); }
    finally { setBusy(false); }
  };

  return <div className="page page--dashboard dashboard-live-event-detail chat-chooses">
    <nav className="dashboard-live-event-detail__navigation" aria-label={t("dashboard.liveEvents.title")}>
      <button ref={backRef} className="dashboard-live-event-back" type="button" onClick={onBack}>
        <ArrowLeft aria-hidden="true" />{t("dashboard.liveEvents.back")}
      </button>
    </nav>
    <section className="dashboard-pesky-battle chat-chooses__panel" data-phase={phase} aria-labelledby="chat-chooses-title">
    <header className="dashboard-pesky-battle__heading">
      <div><p className="dashboard-eyebrow">{t("dashboard.liveEvents.title")}</p>
        <h1 id="chat-chooses-title"><MessageCircleCheck aria-hidden="true" />{t("dashboard.chatChooses.title")}</h1>
        <p>{t("dashboard.chatChooses.description")}</p></div>
      <div className="dashboard-pesky-battle__heading-actions">
        <span className="dashboard-pesky-battle__status" data-phase={phase}>{t(`dashboard.chatChooses.phase.${phase}`)}</span>
      </div>
    </header>

    <div className="chat-chooses__body">
    <p className="chat-chooses__play-help" role={phase === "result" ? "status" : undefined}>
      {t("dashboard.chatChooses.playHelp")}
    </p>
    <div className="chat-chooses__controls">
      <fieldset disabled={locked || busy || !state?.ready}>
        <legend>{t("dashboard.chatChooses.mode")}</legend>
        <div className="chat-chooses__modes">
          {[true, false].map((withChallenge) => <button key={String(withChallenge)} type="button"
            aria-pressed={state?.withChallenge === withChallenge}
            onClick={() => void command("save", withChallenge)}>
            {t(`dashboard.chatChooses.${withChallenge ? "withChallenge" : "withoutChallenge"}`)}
          </button>)}
        </div>
      </fieldset>
      <div className="dashboard-pesky-battle__actions">
        {state?.developmentTools ? <button type="button" className="dashboard-pesky-battle__secondary"
          disabled={busy || phase !== "voting" || state.testVotes?.active}
          title={t("dashboard.chatChooses.testVotesHelp")} onClick={() => void command("test_votes")}>
          {t("dashboard.chatChooses.testVotes")}
        </button> : null}
        {!locked ? <button type="button" className="dashboard-pesky-battle__primary" disabled={busy || !state?.ready || !state.mapAvailable || blocked}
          onClick={() => void command("start")}>{t("dashboard.chatChooses.start")}</button> :
          <button type="button" className="dashboard-pesky-battle__primary" disabled={busy || phase !== "voting"} onClick={() => void command("next")}>
            {phase === "voting" ? t("dashboard.chatChooses.next")
              .replace("{current}", t(`dashboard.chatChooses.stages.${state?.stage}`))
              .replace("{next}", t(`dashboard.chatChooses.stages.${state?.nextStage}`))
              : t(`dashboard.chatChooses.phase.${phase}`)}
          </button>}
        {locked ? <button type="button" className="dashboard-pesky-battle__secondary" disabled={busy}
          onClick={() => void command("stop")}>{t("dashboard.chatChooses.stop")}</button> : null}
      </div>
    </div>
      {state?.developmentTools && state.testVotes?.active ? <p className="chat-chooses__test-progress" role="status">
        {t("dashboard.chatChooses.testVotesProgress").replace("{sent}", String(state.testVotes.sent))
          .replace("{total}", String(state.testVotes.total)).replace("{seconds}", String(state.testVotes.remainingSeconds))}
      </p> : null}
      {blocked ? <p role="status">{t("dashboard.chatChooses.blocked")}</p> : null}
      {(!state?.ready || (!locked && !state.mapAvailable)) ? <p role="status">{t("dashboard.chatChooses.mapRequired")}</p> : null}
      {state?.plane ? <p>{t("dashboard.chatChooses.planeHelp")}</p> : null}
      {(state?.error || requestError) ? <p className="chat-chooses__error" role="alert">
        {t(`dashboard.chatChooses.feedback.${state?.error ? state.feedback : "request_failed"}`,
          t("dashboard.chatChooses.feedback.request_failed"))}</p> : null}

    <ol className="chat-chooses__steps" aria-label={t("dashboard.chatChooses.progress")}>
      {activeStages.map((stage) => <li key={stage} data-current={state?.stage === stage && ["voting", "reveal"].includes(phase)}
        data-selected={Boolean(state?.selected?.[stage])}>
        {state?.selected?.[stage] ? <Check aria-hidden="true" /> : null}
        <span>{t(`dashboard.chatChooses.stages.${stage}`)}</span>
        {state?.selected?.[stage] ? <small>{state.selected[stage]?.name}</small> : null}
      </li>)}
    </ol>

    {phase === "voting" || phase === "reveal" ? <section className="chat-chooses__ballot" aria-label={t(`dashboard.chatChooses.stages.${state?.stage}`)}>
      <div className="chat-chooses__options" style={{ "--option-count": state?.options.length ?? 6 } as CSSProperties}>
        {state?.options?.map((option) => <article key={option.id} title={option.name}
          data-leader={phase === "voting" && highestVotes > 0 && option.votes === highestVotes}
          data-winner={phase === "reveal" && option.number === state.winnerNumber}>
          <div className="chat-chooses__portrait">
            <img src={`/assets/${option.image}`} alt="" />
            <span className="chat-chooses__number">{option.number}</span>
            <span className="chat-chooses__votes" aria-label={votesLabel(option.votes ?? 0)} title={votesLabel(option.votes ?? 0)}>
              {voteCount(option.votes ?? 0)}
            </span>
          </div>
          <strong>{option.name}</strong>
        </article>)}
      </div>
      {phase === "reveal" ? <p role="status">{t(`dashboard.chatChooses.outcome.${state?.outcome}`)}</p> : null}
    </section> : null}
    </div>
    </section>

    <OverlayDesignerCallout componentId="chat_chooses" onOpen={onOpenOverlayDesigner} />
  </div>;
}
