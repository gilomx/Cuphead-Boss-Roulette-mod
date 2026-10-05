import { ArrowLeft, Check, Copy, Vote } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { ChatChoosesStage } from "../../model";
import "../../styles/chat-chooses.css";

const STAGES: ChatChoosesStage[] = ["boss", "weapon1", "weapon2", "super", "charm", "modifier"];

export function ChatChoosesDetailView({ onBack }: { onBack: () => void }) {
  const { chatChooses: state, sendChatChooses, liveEvents } = useConfig();
  const { t, locale } = useLocalization();
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState(false);
  const [requestError, setRequestError] = useState(false);
  const backRef = useRef<HTMLButtonElement>(null);
  const phase = state?.phase ?? "off";
  const locked = phase !== "off" && phase !== "completed";
  const blocked = Boolean(liveEvents?.activeEvent && liveEvents.activeEvent !== "chat_chooses");
  const activeStages = STAGES.filter((stage) => (!state?.plane || !["weapon1", "weapon2", "super"].includes(stage)) &&
    (stage !== "modifier" || state?.withChallenge));
  const overlayPath = `/chat-chooses-overlay?lang=${locale}`;
  const votesLabel = (count: number) => t(`dashboard.chatChooses.${count === 1 ? "vote" : "votes"}`).replace("{count}", String(count));

  useEffect(() => { backRef.current?.focus({ preventScroll: true }); }, []);
  useEffect(() => {
    const keyDown = (event: KeyboardEvent) => { if (event.key === "Escape") onBack(); };
    window.addEventListener("keydown", keyDown);
    return () => window.removeEventListener("keydown", keyDown);
  }, [onBack]);

  const command = async (operation: "save" | "start" | "next" | "stop" | "finish", withChallenge?: boolean) => {
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
    <header className="dashboard-tap-farming__heading">
      <div><p className="dashboard-eyebrow">{t("dashboard.liveEvents.title")}</p>
        <h1><Vote aria-hidden="true" />{t("dashboard.chatChooses.title")}</h1>
        <p>{t("dashboard.chatChooses.description")}</p></div>
      <span className="dashboard-pesky-battle__status">{t(`dashboard.chatChooses.phase.${phase}`)}</span>
    </header>

    <section className="dashboard-panel chat-chooses__controls">
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
      <p>{t("dashboard.chatChooses.voteHelp")}</p>
      {blocked ? <p role="status">{t("dashboard.chatChooses.blocked")}</p> : null}
      {(!state?.ready || (!locked && !state.mapAvailable)) ? <p role="status">{t("dashboard.chatChooses.mapRequired")}</p> : null}
      {state?.plane ? <p>{t("dashboard.chatChooses.planeHelp")}</p> : null}
      <div className="chat-chooses__actions">
        {!locked ? <button type="button" disabled={busy || !state?.ready || !state.mapAvailable || blocked}
          onClick={() => void command("start")}>{t("dashboard.chatChooses.start")}</button> :
          <button type="button" disabled={busy || phase !== "voting"} onClick={() => void command("next")}>
            {phase === "voting" ? t("dashboard.chatChooses.next")
              .replace("{current}", t(`dashboard.chatChooses.stages.${state?.stage}`))
              .replace("{next}", t(`dashboard.chatChooses.stages.${state?.nextStage}`))
              : t(`dashboard.chatChooses.phase.${phase}`)}
          </button>}
        {locked ? <button type="button" className="chat-chooses__secondary" disabled={busy}
          onClick={() => void command("stop")}>{t("dashboard.chatChooses.stop")}</button> : null}
      </div>
      {(state?.error || requestError) ? <p className="chat-chooses__error" role="alert">
        {t(`dashboard.chatChooses.feedback.${state?.error ? state.feedback : "request_failed"}`,
          t("dashboard.chatChooses.feedback.request_failed"))}</p> : null}
    </section>

    <ol className="chat-chooses__steps" aria-label={t("dashboard.chatChooses.progress")}>
      {activeStages.map((stage) => <li key={stage} data-current={state?.stage === stage && ["voting", "reveal"].includes(phase)}
        data-selected={Boolean(state?.selected?.[stage])}>
        {state?.selected?.[stage] ? <Check aria-hidden="true" /> : null}
        <span>{t(`dashboard.chatChooses.stages.${stage}`)}</span>
        {state?.selected?.[stage] ? <small>{state.selected[stage]?.name}</small> : null}
      </li>)}
    </ol>

    {phase === "voting" || phase === "reveal" ? <section className="dashboard-panel">
      <div className="dashboard-panel__heading"><h2>{t(`dashboard.chatChooses.stages.${state?.stage}`)}</h2>
        <span>{votesLabel(state?.totalVotes ?? 0)}</span></div>
      <div className="chat-chooses__options" data-count={state?.options?.length}>
        {state?.options?.map((option) => <article key={option.id} data-winner={phase === "reveal" && option.number === state.winnerNumber}>
          <span className="chat-chooses__number">{option.number}</span>
          <img src={`/assets/${option.image}`} alt="" />
          <strong>{option.name}</strong>
          <progress value={option.votes ?? 0} max={Math.max(1, state.totalVotes)} aria-label={option.name} />
          <small>{votesLabel(option.votes ?? 0)}
            {` · ${state.totalVotes ? Math.round((option.votes ?? 0) / state.totalVotes * 100) : 0}%`}</small>
        </article>)}
      </div>
      {phase === "reveal" ? <p role="status">{t(`dashboard.chatChooses.outcome.${state?.outcome}`)}</p> : null}
    </section> : null}

    <section className="dashboard-panel chat-chooses__overlay">
      <div className="dashboard-panel__heading"><h2>{t("dashboard.chatChooses.overlay")}</h2>
        <button type="button" className="chat-chooses__secondary" onClick={() => {
          void navigator.clipboard.writeText(`${window.location.origin}${overlayPath}`)
            .then(() => { setCopied(true); window.setTimeout(() => setCopied(false), 2000); })
            .catch(() => setRequestError(true));
        }}><Copy aria-hidden="true" />{t(`dashboard.chatChooses.${copied ? "copied" : "copyUrl"}`)}</button></div>
      <p>{t("dashboard.chatChooses.overlayHelp")}</p>
      <div className="chat-chooses__preview"><iframe src={`${overlayPath}&preview=1`} title={t("dashboard.chatChooses.overlay")} /></div>
      <a href={overlayPath} target="_blank" rel="noreferrer">{t("dashboard.chatChooses.openOverlay")}</a>
    </section>
  </div>;
}
