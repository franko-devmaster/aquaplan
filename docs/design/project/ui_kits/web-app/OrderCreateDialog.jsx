// Order-create dialog — implements the "Formulaire dans dialog" pattern
// described in the context doc. Includes a round-section (conditional
// framed block) to demonstrate that pattern.

function OrderCreateDialog({ open, onClose, onSubmit }) {
  const [loc, setLoc] = React.useState("");
  const [program, setProgram] = React.useState("");
  const [scheduleMode, setScheduleMode] = React.useState("asap"); // asap | scheduled
  const [date, setDate] = React.useState("");
  const [slot, setSlot] = React.useState("08:00–12:00");

  if (!open) return null;

  return (
    <div style={{
      position: "fixed", inset: 0, background: "rgba(17,47,66,0.35)",
      display: "flex", alignItems: "center", justifyContent: "center",
      zIndex: 1050, padding: 24,
    }} onClick={onClose}>
      <div onClick={e => e.stopPropagation()} style={{
        width: 560, background: "#fff", borderRadius: 8,
        boxShadow: "0 4px 8px rgba(17,47,66,0.08), 0 8px 20px rgba(17,47,66,0.10)",
        display: "flex", flexDirection: "column", maxHeight: "90vh",
      }}>
        {/* Title */}
        <div style={{ padding: "20px 24px 12px" }}>
          <h2 style={{ margin: 0, fontSize: 20, fontWeight: 600 }}>Nouvel ordre d'analyse</h2>
        </div>
        {/* Content */}
        <div style={{ padding: "8px 24px 16px", overflow: "auto", display: "flex", flexDirection: "column", gap: 16 }}>
          <SectionLabel>Informations générales</SectionLabel>
          <Field label="Lieu de prélèvement" icon="place" value={loc} onChange={setLoc}
                 placeholder="Rechercher un lieu…"/>
          <Field label="Programme d'analyse" icon="science" value={program} onChange={setProgram}
                 placeholder="P-12 — Eau potable, contrôle trimestriel"/>

          <SectionLabel>Planification</SectionLabel>
          <div style={{ display: "flex", gap: 20 }}>
            {[["asap", "Dès que possible"], ["scheduled", "Date précise"]].map(([id, lbl]) => (
              <label key={id} style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 14, cursor: "pointer" }}>
                <input type="radio" name="sched" checked={scheduleMode === id}
                       onChange={() => setScheduleMode(id)}
                       style={{ accentColor: "#1f7aa6" }}/>
                {lbl}
              </label>
            ))}
          </div>

          {/* Round-section — conditional framed block */}
          {scheduleMode === "scheduled" && (
            <div style={{
              border: "1px solid #dfdfdc", borderRadius: 8, padding: 14,
              display: "flex", flexDirection: "column", gap: 12,
            }}>
              <SectionLabel>Créneau prévu</SectionLabel>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <Field label="Date" icon="calendar_today" value={date} onChange={setDate}
                       placeholder="24.04.2026"/>
                <Field label="Créneau" icon="schedule" value={slot} onChange={setSlot}/>
              </div>
            </div>
          )}
        </div>
        {/* Actions */}
        <div style={{
          display: "flex", justifyContent: "flex-end", gap: 8,
          padding: "12px 24px 20px", borderTop: "1px solid #ececea",
        }}>
          <Button variant="text" onClick={onClose}>Annuler</Button>
          <Button variant="primary" onClick={() => { onSubmit?.({ loc, program, scheduleMode, date, slot }); onClose(); }}>
            Créer l'ordre
          </Button>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { OrderCreateDialog });
