// Shared primitives for the Aquaplan UI kit.
// Components consume the design tokens via var(--token). Keep styles inline
// (mirrors the repo's standalone + inline-styles convention).

const { useState } = React;

/* --------------------------- Status chip ----------------------------------
   Mirrors <app-status-chip>. Maps a metier status id to (bg, fg).
   ------------------------------------------------------------------------ */
const STATUS_MAP = {
  to_plan:     { label: "À planifier",  bg: "#ececea", fg: "#4f4f4c" },
  planned:     { label: "Planifié",     bg: "#dff3f1", fg: "#1f6b66" },
  in_progress: { label: "En cours",     bg: "#e0edf8", fg: "#134a78" },
  sampled:     { label: "Prélevé",      bg: "#fae4eb", fg: "#973057" },
  in_analysis: { label: "En analyse",   bg: "#ebe3f6", fg: "#5b3da0" },
  results:     { label: "Résultats",    bg: "#fcefcf", fg: "#855d08" },
  closed:      { label: "Clôturé",      bg: "#dfeee1", fg: "#1e5c32" },
  cancelled:   { label: "Annulé",       bg: "#2a2a2a", fg: "#ffffff" },
  draft:       { label: "Draft",        bg: "#f3f3f1", fg: "#4f4f4c" },
  assigned:    { label: "Assigned",     bg: "#e0edf8", fg: "#134a78" },
  conform:     { label: "Conforme",     bg: "#dfeee1", fg: "#1e5c32" },
  non_conform: { label: "Non conforme", bg: "#fae4eb", fg: "#973057" },
  pending:     { label: "En attente",   bg: "#fcefcf", fg: "#855d08" },
};

function StatusChip({ status, label }) {
  const s = STATUS_MAP[status] || { label: status, bg: "#ececea", fg: "#4f4f4c" };
  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: 6,
      height: 24, padding: "0 10px",
      borderRadius: 6, fontSize: 12, fontWeight: 500,
      background: s.bg, color: s.fg, whiteSpace: "nowrap",
    }}>
      <span style={{
        width: 6, height: 6, borderRadius: "50%",
        background: "currentColor", opacity: 0.75,
      }}/>
      {label ?? s.label}
    </span>
  );
}

/* --------------------------- Button --------------------------------------- */
function Button({ variant = "primary", icon, children, onClick, disabled, fullWidth, size = "md" }) {
  const h = size === "lg" ? 48 : size === "sm" ? 36 : 44;
  const base = {
    display: "inline-flex", alignItems: "center", justifyContent: "center", gap: 6,
    height: h, padding: "0 16px", borderRadius: 4, border: "none",
    font: "500 14px var(--font-family-base)", cursor: disabled ? "not-allowed" : "pointer",
    opacity: disabled ? 0.5 : 1, width: fullWidth ? "100%" : undefined,
    transition: "background 120ms cubic-bezier(0.2,0,0.1,1)",
  };
  const variants = {
    primary: { background: "#1f7aa6", color: "#fff" },
    stroked: { background: "#fff", color: "#166389", border: "1px solid #166389" },
    text:    { background: "transparent", color: "#166389" },
    danger:  { background: "#d94b3d", color: "#fff" },
  };
  return (
    <button style={{ ...base, ...variants[variant] }} onClick={onClick} disabled={disabled}>
      {icon && <span className="material-icons-outlined" style={{ fontSize: 18 }}>{icon}</span>}
      {children}
    </button>
  );
}

/* --------------------------- Form field ----------------------------------- */
function Field({ label, icon, value, onChange, type = "text", error, hint, disabled, placeholder }) {
  const [focused, setFocused] = useState(false);
  const border = error ? "#d94b3d" : focused ? "#1f7aa6" : "#dfdfdc";
  const ring = focused && !error ? "0 0 0 3px rgba(31,122,166,0.25)" : "none";
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      {label && <span style={{ fontSize: 12, color: "#6b6b67" }}>{label}</span>}
      <div style={{
        display: "flex", alignItems: "center", gap: 8,
        height: 44, border: `1px solid ${border}`, borderRadius: 4,
        padding: "0 10px", boxShadow: ring,
        background: disabled ? "#f5f5f4" : "#fff",
        color: disabled ? "#6b6b67" : "inherit",
        transition: "border-color 120ms, box-shadow 120ms",
      }}>
        {icon && <span className="material-icons-outlined" style={{
          fontSize: 18, color: error ? "#b33427" : "#6b6b67"
        }}>{icon}</span>}
        <input
          type={type} value={value ?? ""} placeholder={placeholder}
          disabled={disabled}
          onChange={e => onChange?.(e.target.value)}
          onFocus={() => setFocused(true)} onBlur={() => setFocused(false)}
          style={{
            border: "none", outline: "none", flex: 1, background: "transparent",
            font: "400 14px var(--font-family-base)", color: "inherit",
          }}
        />
      </div>
      {error ? <span style={{ fontSize: 12, color: "#b33427" }}>{error}</span>
             : hint ? <span style={{ fontSize: 12, color: "#6b6b67" }}>{hint}</span>
             : null}
    </div>
  );
}

/* --------------------------- Card ----------------------------------------- */
function Card({ clickable, onClick, children, style }) {
  return (
    <div onClick={onClick} style={{
      background: "#fff",
      border: "1px solid #dfdfdc",
      borderRadius: 8,
      padding: 14,
      boxShadow: clickable ? "0 1px 2px rgba(17,47,66,0.06), 0 1px 3px rgba(17,47,66,0.08)" : "none",
      cursor: clickable ? "pointer" : "default",
      ...style,
    }}>
      {children}
    </div>
  );
}

/* --------------------------- Page header --------------------------------- */
function PageHeader({ title, count, action }) {
  return (
    <div style={{
      display: "flex", justifyContent: "space-between", alignItems: "center",
      paddingBottom: 12, borderBottom: "1px solid #dfdfdc", marginBottom: 16,
    }}>
      <div style={{ display: "flex", alignItems: "baseline", gap: 10 }}>
        <h2 style={{ margin: 0, fontSize: 22, fontWeight: 600 }}>{title}</h2>
        {count != null && <span style={{ fontSize: 13, color: "#6b6b67" }}>· {count}</span>}
      </div>
      {action}
    </div>
  );
}

/* --------------------------- Section label ------------------------------- */
function SectionLabel({ children }) {
  return <div style={{
    fontSize: 12, fontWeight: 600, color: "#6b6b67",
    letterSpacing: "0.04em", textTransform: "uppercase", marginBottom: 8,
  }}>{children}</div>;
}

Object.assign(window, { StatusChip, Button, Field, Card, PageHeader, SectionLabel, STATUS_MAP });
