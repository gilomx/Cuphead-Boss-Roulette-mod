import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import { ConfigProvider } from "./config/ConfigContext";
import { LocalizationProvider } from "./i18n/LocalizationContext";
import "./styles/index.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <LocalizationProvider>
      <ConfigProvider>
        <App />
      </ConfigProvider>
    </LocalizationProvider>
  </StrictMode>,
);
