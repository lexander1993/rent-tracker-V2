import { Fragment, useEffect, useState } from "react";
import "./App.css";

// The page is served by the API, so it talks to the same address.
const API = "";

const PAGES = [
  { id: "dashboard", label: "Dashboard" },
  { id: "renters", label: "Add tenant" },
  { id: "payment", label: "Record payment" },
  { id: "status", label: "Payment History" },
  { id: "statement", label: "Statement" },
  { id: "places", label: "Properties & rooms" },
  { id: "expenses", label: "Expenses" },
  { id: "report", label: "Report" },
];

// One colour for each residence, so rooms are easy to tell apart.
const PROPERTY_COLORS = ["#1E6FBF", "#059669", "#B45309", "#7C3AED", "#DB2777", "#0E7490"];

const RENTER_STATUS = { CURRENT: "Current tenant", MOVED_OUT: "Moved out" };

const ROOM_STATUS = { AVAILABLE: "Available", REPAIR: "Under repair", OFF_MARKET: "Not for rent" };

// The 1st of this month, up to today.
function monthToToday() {
  const today = todayDate();
  return { from: today.slice(0, 8) + "01", to: today };
}

// Whole days from a "YYYY-MM-DD" date to today.
function daysSince(ymd) {
  if (!ymd) return null;
  const [y, m, d] = ymd.split("-").map(Number);
  const then = Date.UTC(y, m - 1, d);
  const now = new Date();
  const today = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  return Math.round((today - then) / 86400000);
}

// Keeps digits and one decimal point with up to 2 cents digits: "1,200.5x" -> "1200.5".
function cleanMoney(text) {
  const [whole, ...rest] = String(text ?? "").replace(/[^0-9.]/g, "").split(".");
  return rest.length ? `${whole}.${rest.join("").slice(0, 2)}` : whole;
}

// A dollar amount box. It shows commas as you type, and keeps the plain number as its value.
function MoneyInput({ value, onChange, required }) {
  const clean = cleanMoney(value);
  const [whole, cents] = clean.split(".");
  const shown = clean === "" ? "" : (whole ? Number(whole).toLocaleString("en-US") : "0") + (cents !== undefined ? `.${cents}` : "");
  return (
    <div className="money-input">
      <span className="money-prefix">$</span>
      <input
        inputMode="decimal"
        placeholder="0"
        value={shown}
        required={required}
        onChange={(e) => onChange(cleanMoney(e.target.value))}
      />

    </div>
  );
}

// A box that pops up over the page.
function Modal({ title, onClose, children }) {
  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
        <div className="modal-hdr">
          <div className="card-title">{title}</div>
          <button className="btn btn-outline btn-sm" onClick={onClose}>
            Close
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

const pillClass = {
  PAID: "pill pill-paid",
  PARTIAL: "pill pill-partial",
  "NOT PAID": "pill pill-unpaid",
};

// ---------- Helpers ----------

function todayDate() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
const thisMonth = () => todayDate().slice(0, 7);

// Financial year 2026 means 1 July 2026 to 30 June 2027.
function currentFyYear() {
  const d = new Date();
  return d.getMonth() >= 6 ? d.getFullYear() : d.getFullYear() - 1;
}
const fyFrom = (y) => `${y}-07-01`;
const fyTo = (y) => `${y + 1}-06-30`;
const fyLabel = (y) => `FY${y}-${String(y + 1).slice(-2)}`;
const fyRange = (y) => ({ from: fyFrom(y), to: fyTo(y) });

function rangePresets() {
  const y = new Date().getFullYear();
  return [
    { label: "This financial year", range: fyRange(currentFyYear()) },
    { label: "Last financial year", range: fyRange(currentFyYear() - 1) },
    { label: "This calendar year", range: { from: `${y}-01-01`, to: `${y}-12-31` } },
  ];
}

// Dollars, with cents only when the amount has them: $1,200 or $610.50. Negatives show as -$360.
const money = (n) => {
  const v = Number(n) || 0;
  const cents = Math.round(Math.abs(v) * 100) % 100 !== 0;
  const text = Math.abs(v).toLocaleString("en-US", {
    minimumFractionDigits: cents ? 2 : 0,
    maximumFractionDigits: cents ? 2 : 0,
  });
  return (v < 0 ? "-$" : "$") + text;
};

function pageFromHash() {
  const id = window.location.hash.replace("#", "");
  return PAGES.some((p) => p.id === id) ? id : "dashboard";
}

async function request(path, options = {}) {
  const res = await fetch(API + path, {
    headers: { "Content-Type": "application/json" },
    ...options,
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    const err = new Error(data.error || `Request failed (${res.status})`);
    err.status = res.status;
    throw err;
  }
  return data;
}

const emptyRenter = { name: "", dob: "" };

const MAX_PHOTO = 5 * 1024 * 1024;
const statePill = { PAID: "pill-paid", PARTIAL: "pill-partial", "NOT PAID": "pill-unpaid" };
const monthLabel = (ym) => {
  const [y, m] = ym.split("-").map(Number);
  return new Date(y, m - 1, 1).toLocaleString("en-US", { month: "long", year: "numeric" });
};
const shortMonth = (ym) => {
  const [y, m] = ym.split("-").map(Number);
  return new Date(y, m - 1, 1).toLocaleString("en-US", { month: "short" });
};
const compactMoney = (v) => "$" + (v >= 1e6 ? `${(v / 1e6).toFixed(1)}M` : v >= 1e3 ? `${Math.round(v / 1e3)}k` : String(Math.round(v)));

// Reads a JPG or PNG from the computer and returns it as a data address.
function readPhotoFile(file) {
  return new Promise((resolve, reject) => {
    if (!["image/jpeg", "image/png"].includes(file.type)) return reject(new Error("The ID photo must be a JPG or PNG file."));
    if (file.size > MAX_PHOTO) return reject(new Error("The ID photo must be under 5 MB."));
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = () => reject(new Error("Could not read that file."));
    reader.readAsDataURL(file);
  });
}

// Bars: rent due and collected for each of the last 12 months.
// Line: collection rate (% of due that was paid), read on the right-hand axis.
function TrendChart({ data, selectedMonth }) {
  const W = 720;
  const H = 280;
  const pad = { t: 24, r: 44, b: 36, l: 56 };
  const max = Math.max(1, ...data.flatMap((d) => [d.due, d.collected]));
  const innerW = W - pad.l - pad.r;
  const innerH = H - pad.t - pad.b;
  const base = pad.t + innerH;
  const slot = innerW / data.length;
  const barW = Math.min(18, slot * 0.36);
  const y = (v) => pad.t + innerH - (v / max) * innerH;
  const yPct = (p) => pad.t + innerH - (p / 100) * innerH;
  const ticks = [0, 0.25, 0.5, 0.75, 1].map((f) => max * f);
  const centre = (i) => pad.l + slot * i + slot / 2;
  const linePts = data
    .map((d, i) => (d.rate == null ? null : `${centre(i)},${yPct(d.rate)}`))
    .filter(Boolean)
    .join(" ");

  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="chart" role="img" aria-label="Rent due, collected and collection rate for the last 12 months">
      {ticks.map((t) => (
        <g key={t}>
          <line x1={pad.l} x2={W - pad.r} y1={y(t)} y2={y(t)} className="grid-line" />
          <text x={pad.l - 8} y={y(t) + 4} textAnchor="end" className="axis-label">
            {compactMoney(t)}
          </text>
        </g>
      ))}
      {[0, 50, 100].map((p) => (
        <text key={p} x={W - pad.r + 6} y={yPct(p) + 4} textAnchor="start" className="axis-label" style={{ fill: "var(--accent)" }}>
          {p}%
        </text>
      ))}
      {data.map((d, i) => {
        const cx = centre(i);
        const isSelected = d.month === selectedMonth;
        return (
          <g key={d.month}>
            <rect x={cx - barW - 2} y={y(d.due)} width={barW} height={base - y(d.due)} rx="3" className="bar-due">
              <title>{`${monthLabel(d.month)}\nDue ${money(d.due)}`}</title>
            </rect>
            <rect x={cx + 2} y={y(d.collected)} width={barW} height={base - y(d.collected)} rx="3" className="bar-paid">
              <title>{`${monthLabel(d.month)}\nCollected ${money(d.collected)}\nStill owed ${money(d.outstanding)}\nCollection rate ${d.rate == null ? "n/a" : d.rate + "%"}`}</title>
            </rect>
            {d.collected > 0 && (
              <text x={cx + 2 + barW / 2} y={y(d.collected) - 5} textAnchor="middle" className="axis-label">
                {compactMoney(d.collected)}
              </text>
            )}
            {d.rate != null && (
              <g>
                <circle cx={cx} cy={yPct(d.rate)} r="4" fill="var(--accent)">
                  <title>{`${monthLabel(d.month)}: ${d.rate}% collected`}</title>
                </circle>
                <text x={cx} y={yPct(d.rate) - 9} textAnchor="middle" className="axis-label" style={{ fill: "var(--accent)", fontWeight: 700 }}>
                  {d.rate}%
                </text>
              </g>
            )}
            <text
              x={cx}
              y={H - 12}
              textAnchor="middle"
              className="axis-label"
              style={isSelected ? { fontWeight: 700, fill: "var(--navy)" } : undefined}
            >
              {shortMonth(d.month)}
            </text>
          </g>
        );
      })}
      {linePts && <polyline points={linePts} fill="none" stroke="var(--accent)" strokeWidth="2" />}
    </svg>
  );
}

// Colours for rent states, used by the heatmap and the room map.
const STATE_COLOR = { PAID: "#059669", PARTIAL: "#D97706", "NOT PAID": "#DC2626" };
const ROOM_STATE = {
  VACANT: { label: "Vacant", bg: "#94A3B8" },
  PAID: { label: "Paid", bg: "#059669" },
  DUE_SOON: { label: "Due soon", bg: "#D97706" },
  OVERDUE: { label: "Overdue", bg: "#DC2626" },
  OPEN: { label: "Unpaid", bg: "#1E6FBF" },
  UPCOMING: { label: "Moving in", bg: "#7C3AED" },
};
// Which colour a room tile gets, from its room record.
const roomKey = (r) => (!r.tenancyId ? "VACANT" : r.rentState || "OPEN");

// Ring showing the share of rooms that are occupied.
function Donut({ value, total, color }) {
  const r = 40;
  const c = 2 * Math.PI * r;
  const share = total ? value / total : 0;
  return (
    <svg viewBox="0 0 100 100" width="110" height="110" role="img" aria-label={`${value} of ${total} rooms occupied`}>
      <circle cx="50" cy="50" r={r} fill="none" stroke="var(--border)" strokeWidth="12" />
      <circle
        cx="50"
        cy="50"
        r={r}
        fill="none"
        stroke={color || "var(--accent)"}
        strokeWidth="12"
        strokeLinecap="round"
        strokeDasharray={`${share * c} ${c}`}
        transform="rotate(-90 50 50)"
      />
      <text x="50" y="56" textAnchor="middle" fontSize="20" fontWeight="800" fill="var(--navy)">
        {Math.round(share * 100)}%
      </text>
    </svg>
  );
}

// Grid of tenants by month. Each box is green (paid), amber (partly paid) or red (unpaid).
function Heatmap({ data }) {
  if (!data) return null;
  if (data.rows.length === 0) return <p className="empty">No tenants yet.</p>;
  return (
    <div className="table-wrap">
      <table className="heatmap">
        <thead>
          <tr>
            <th>Tenant</th>
            {data.months.map((m) => (
              <th key={m} className="hm-head">
                {shortMonth(m)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.rows.map((row) => (
            <tr key={row.tenancyId}>
              <td className="hm-name">
                <strong>{row.name}</strong>
                <div className="muted-text">{row.place}</div>
              </td>
              {row.cells.map((c, i) =>
                c ? (
                  <td key={i} className="hm-cell">
                    <span
                      className="hm-box"
                      style={{ background: STATE_COLOR[c.state] }}
                      title={`${monthLabel(c.month)} · ${c.state} · ${money(c.paid)} of ${money(c.due)}`}
                    />
                  </td>
                ) : (
                  <td key={i} className="hm-cell hm-off" />
                )
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ---------- Small pieces ----------

function MonthPicker({ value, onChange }) {
  return (
    <div className="inline-field">
      <label>Month</label>
      <input type="month" value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}

function YearPicker({ value, onChange }) {
  const now = currentFyYear();
  const years = [];
  for (let y = now - 3; y <= now + 1; y++) years.push(y);
  return (
    <div className="inline-field">
      <label>Financial year</label>
      <select value={value} onChange={(e) => onChange(Number(e.target.value))}>
        {years.map((y) => (
          <option key={y} value={y}>
            {fyLabel(y)}
          </option>
        ))}
      </select>
    </div>
  );
}

// ---------- App ----------

export default function App() {
  const [page, setPage] = useState(pageFromHash);
  const [error, setError] = useState("");
  const [tick, setTick] = useState(0);

  // Lists used by several pages
  const [renters, setRenters] = useState([]);
  const [properties, setProperties] = useState([]);
  const [rooms, setRooms] = useState([]);
  const [tenancies, setTenancies] = useState([]);
  const [categories, setCategories] = useState([]);

  // Page data
  const [month, setMonth] = useState(thisMonth());
  const [status, setStatus] = useState([]);
  const [allPayments, setAllPayments] = useState([]);
  const [fyYear, setFyYear] = useState(currentFyYear());
  const [expenses, setExpenses] = useState([]);
  const [range, setRange] = useState(monthToToday());
  const [reportTab, setReportTab] = useState("profit");
  const [profitReport, setProfitReport] = useState(null);
  const [expensesReport, setExpensesReport] = useState(null);
  const [monthlyReport, setMonthlyReport] = useState(null);
  const [dash, setDash] = useState(null);
  const [dueSoon, setDueSoon] = useState([]); // rent due in the next 7 days
  const [heatmap, setHeatmap] = useState(null); // tenants by month, for the rent heatmap
  const [stmtRenter, setStmtRenter] = useState("");
  const [stmtRange, setStmtRange] = useState(fyRange(currentFyYear()));
  const [statement, setStatement] = useState(null);

  // Forms
  const [rf, setRf] = useState(emptyRenter);
  const [rPhoto, setRPhoto] = useState(null); // ID photo for the renter being added
  const [photoKey, setPhotoKey] = useState(0); // resets the file box after saving
  const [pf, setPf] = useState({ renterId: "", month: thisMonth(), amount: "", paidDate: todayDate() });
  const [propForm, setPropForm] = useState({ name: "", address: "" });
  const [roomForm, setRoomForm] = useState({ propertyId: "", name: "", askingRent: "" });
  const [roomPrice, setRoomPrice] = useState(""); // rental price in the Update panel
  const [payMonth, setPayMonth] = useState(thisMonth()); // defaults to this month; "" = all months
  const [editingPaymentId, setEditingPaymentId] = useState(null);
  const [paymentEdit, setPaymentEdit] = useState({ renterId: "", month: "", amount: "", paidDate: "" });
  const [expForm, setExpForm] = useState({ propertyId: "", date: todayDate(), category: "", amount: "", note: "" });
  const [editingRoomId, setEditingRoomId] = useState(null); // room whose Update panel is open
  const [renterFilter, setRenterFilter] = useState("ALL");
  const [renterEdit, setRenterEdit] = useState(null); // renter open in the Edit popup
  const [renterError, setRenterError] = useState("");
  const [roomAssign, setRoomAssign] = useState({ renterId: "", startDate: todayDate(), monthlyRent: "", dueDay: "1", endDate: todayDate() });
  const [roomTerms, setRoomTerms] = useState({ startDate: "", monthlyRent: "", dueDay: "1" }); // current tenancy in the Update panel
  const [roomSwap, setRoomSwap] = useState({ renterId: "", startDate: todayDate(), monthlyRent: "", dueDay: "1" }); // change tenant in the Update panel

  useEffect(() => {
    const onHash = () => setPage(pageFromHash());
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);

  function handleError(e) {
    setError(e.message);
  }

  async function loadBase() {
    const [r, p, rm, t, c] = await Promise.all([
      request("/renters"),
      request("/properties"),
      request("/rooms"),
      request("/tenancies"),
      request("/categories"),
    ]);
    setRenters(r);
    setProperties(p);
    setRooms(rm);
    setTenancies(t);
    setCategories(c);
    const firstRenter = r[0] ? String(r[0].id) : "";
    setPf((f) => ({ ...f, renterId: f.renterId || firstRenter }));
    setStmtRenter((prev) => prev || firstRenter);
  }

  async function loadPage() {
    if (page === "dashboard") {
      setDash(await request(`/dashboard?month=${month}`));
      setDueSoon(await request("/due-soon"));
      setHeatmap(await request(`/heatmap?month=${month}`));
    }
    if (page === "statement" && stmtRenter && stmtRange.from && stmtRange.to) {
      setStatement(await request(`/renters/${stmtRenter}/statement?from=${stmtRange.from}&to=${stmtRange.to}`));
    }
    if (page === "status") setStatus(await request(`/status?month=${month}`));
    if (page === "payment") setAllPayments(await request("/payments"));
    if (page === "expenses") {
      setExpenses(await request(`/expenses?from=${fyFrom(fyYear)}&to=${fyTo(fyYear)}`));
    }
    if (page === "report") {
      const q = `from=${range.from}&to=${range.to}`;
      if (reportTab === "profit") setProfitReport(await request(`/reports/profit?${q}`));
      else if (reportTab === "monthly") setMonthlyReport(await request(`/reports/monthly?${q}`));
      else setExpensesReport(await request(`/reports/expenses?${q}`));
    }
  }

  useEffect(() => {
    setError("");
    loadBase().then(loadPage).catch(handleError);
  }, [page, month, stmtRenter, stmtRange.from, stmtRange.to, fyYear, range.from, range.to, reportTab, tick]);

  function refresh() {
    setTick((t) => t + 1);
  }

  // Runs a save or delete, shows any error, then reloads the data.
  async function run(action) {
    try {
      setError("");
      await action();
      refresh();
    } catch (e) {
      handleError(e);
    }
  }

  // ----- Actions -----

  function addRenter(e) {
    e.preventDefault();
    run(async () => {
      const created = await request("/renters", { method: "POST", body: JSON.stringify(rf) });
      if (rPhoto) {
        await request(`/renters/${created.id}/photo`, { method: "PUT", body: JSON.stringify({ dataUrl: rPhoto }) });
      }
      setRf(emptyRenter);
      setRPhoto(null);
      setPhotoKey((k) => k + 1);
    });
  }

  async function choosePhoto(e) {
    const file = e.target.files[0];
    setError("");
    if (!file) return setRPhoto(null);
    try {
      setRPhoto(await readPhotoFile(file));
    } catch (err) {
      setError(err.message);
      setRPhoto(null);
      setPhotoKey((k) => k + 1);
    }
  }

  function uploadPhoto(renterId, file) {
    run(async () => {
      const dataUrl = await readPhotoFile(file);
      await request(`/renters/${renterId}/photo`, { method: "PUT", body: JSON.stringify({ dataUrl }) });
    });
  }

  function removePhoto(renterId) {
    if (!confirm("Remove this ID photo?")) return;
    run(() => request(`/renters/${renterId}/photo`, { method: "DELETE" }));
  }

  function deleteRenter(id) {
    if (!confirm("Delete this tenant? This only works if they have no tenancies or payments.")) return;
    run(() => request(`/renters/${id}`, { method: "DELETE" }));
  }

  function addPayment(e) {
    e.preventDefault();
    run(async () => {
      await request("/payments", {
        method: "POST",
        body: JSON.stringify({ ...pf, renterId: Number(pf.renterId), amount: Number(pf.amount) }),
      });
      setPf((f) => ({ ...f, amount: "" }));
    });
  }

  function openPaymentEditor(p) {
    setEditingPaymentId((current) => (current === p.id ? null : p.id));
    setPaymentEdit({
      renterId: String(p.renterId),
      month: p.month,
      amount: String(Number(p.amount)),
      paidDate: p.paidDate,
    });
  }

  function savePayment(e) {
    e.preventDefault();
    run(async () => {
      await request(`/payments/${editingPaymentId}`, {
        method: "PATCH",
        body: JSON.stringify({ ...paymentEdit, renterId: Number(paymentEdit.renterId), amount: Number(paymentEdit.amount) }),
      });
      setEditingPaymentId(null);
    });
  }

  function deletePayment(id) {
    if (!confirm("Delete this payment?")) return;
    run(() => request(`/payments/${id}`, { method: "DELETE" }));
  }

  function addProperty(e) {
    e.preventDefault();
    run(async () => {
      await request("/properties", { method: "POST", body: JSON.stringify(propForm) });
      setPropForm({ name: "", address: "" });
    });
  }

  function deleteProperty(id) {
    if (!confirm("Delete this property? This only works if it has no rooms or expenses.")) return;
    run(() => request(`/properties/${id}`, { method: "DELETE" }));
  }

  function addRoom(e) {
    e.preventDefault();
    run(async () => {
      await request("/rooms", {
        method: "POST",
        body: JSON.stringify({
          propertyId: Number(roomForm.propertyId),
          name: roomForm.name,
          askingRent: roomForm.askingRent ? Number(roomForm.askingRent) : null,
        }),
      });
      setRoomForm((f) => ({ ...f, name: "", askingRent: "" }));
    });
  }

  function saveRoom(id, body) {
    run(() => request(`/rooms/${id}`, { method: "PATCH", body: JSON.stringify(body) }));
  }

  function openRenterEditor(r) {
    setRenterError("");
    setRenterEdit({ id: r.id, name: r.name, dob: r.dob, status: r.status });
  }

  async function saveRenterEdit(e) {
    e.preventDefault();
    setRenterError("");
    try {
      await request(`/renters/${renterEdit.id}`, {
        method: "PATCH",
        body: JSON.stringify({ name: renterEdit.name, dob: renterEdit.dob, status: renterEdit.status }),
      });
      setRenterEdit(null);
      refresh();
    } catch (err) {
      setRenterError(err.message);
    }
  }

  // Opens or closes the Update panel for a room.
  function openRoomEditor(room) {
    setEditingRoomId((current) => (current === room.id ? null : room.id));
    setRoomPrice(room.askingRent == null ? "" : String(Number(room.askingRent)));
    setRoomAssign({ renterId: "", startDate: todayDate(), monthlyRent: "", dueDay: "1", endDate: todayDate() });
    setRoomTerms({
      startDate: room.movedIn || todayDate(),
      monthlyRent: room.rent == null ? "" : String(Number(room.rent)),
      dueDay: String(room.dueDay ?? 1),
    });
    setRoomSwap({ renterId: "", startDate: todayDate(), monthlyRent: room.rent == null ? "" : String(Number(room.rent)), dueDay: String(room.dueDay ?? 1) });
  }

  // Saves the move-in date, monthly rent or due day of the current tenancy.
  function saveRoomTerms(tenancyId, e) {
    e.preventDefault();
    run(async () => {
      await request(`/tenancies/${tenancyId}`, {
        method: "PATCH",
        body: JSON.stringify({
          startDate: roomTerms.startDate,
          monthlyRent: Number(roomTerms.monthlyRent),
          dueDay: Number(roomTerms.dueDay),
        }),
      });
      setEditingRoomId(null);
    });
  }

  // Ends the current tenancy on the new tenant's move-in date and starts the new one.
  function changeRoomTenant(tenancyId, e) {
    e.preventDefault();
    run(async () => {
      await request(`/tenancies/${tenancyId}/change-tenant`, {
        method: "POST",
        body: JSON.stringify({
          renterId: Number(roomSwap.renterId),
          startDate: roomSwap.startDate,
          monthlyRent: Number(roomSwap.monthlyRent),
          dueDay: Number(roomSwap.dueDay),
        }),
      });
      setEditingRoomId(null);
    });
  }

  function assignRoomRenter(e) {
    e.preventDefault();
    run(async () => {
      await request("/tenancies", {
        method: "POST",
        body: JSON.stringify({
          roomId: editingRoomId,
          renterId: Number(roomAssign.renterId),
          startDate: roomAssign.startDate,
          endDate: null,
          monthlyRent: Number(roomAssign.monthlyRent),
          dueDay: Number(roomAssign.dueDay),
        }),
      });
      setEditingRoomId(null);
    });
  }

  function endRoomTenancy(tenancyId) {
    if (!confirm(`End this tenancy on ${roomAssign.endDate}?`)) return;
    run(async () => {
      await request(`/tenancies/${tenancyId}/end`, { method: "PATCH", body: JSON.stringify({ endDate: roomAssign.endDate }) });
      setEditingRoomId(null);
    });
  }

  // Each residence gets its own colour, by the order it was added.
  const propertyColor = (propertyId) =>
    PROPERTY_COLORS[Math.max(0, properties.findIndex((p) => p.id === propertyId)) % PROPERTY_COLORS.length];

  function deleteRoom(id) {
    if (!confirm("Delete this room? Its past tenancy records will be removed. Rent payments are kept.")) return;
    run(() => request(`/rooms/${id}`, { method: "DELETE" }));
  }

  function addExpense(e) {
    e.preventDefault();
    run(async () => {
      await request("/expenses", {
        method: "POST",
        body: JSON.stringify({ ...expForm, propertyId: Number(expForm.propertyId), amount: Number(expForm.amount) }),
      });
      setExpForm((f) => ({ ...f, amount: "", note: "" }));
    });
  }

  function deleteExpense(id) {
    if (!confirm("Delete this expense?")) return;
    run(() => request(`/expenses/${id}`, { method: "DELETE" }));
  }

  // ----- Views -----

  const currentLabel = PAGES.find((p) => p.id === page)?.label;
  const today = todayDate();
  const shownPayments = payMonth ? allPayments.filter((p) => p.month === payMonth) : allPayments;
  const shownTotal = shownPayments.reduce((sum, p) => sum + p.amount, 0);
  const expenseTotal = expenses.reduce((sum, e) => sum + e.amount, 0);

  return (
    <>
      <header className="topbar">
        <div className="topbar-inner">
          <div className="brand">
            <div className="brand-mark">RT</div>
            <div className="brand-text">
              <span className="brand-name">Rent Tracker</span>
              <span className="brand-sub">Your rent records</span>
            </div>
          </div>

          <nav className="nav">
            {PAGES.map((p) => (
              <a key={p.id} href={`#${p.id}`} className={`nav-link${page === p.id ? " active" : ""}`}>
                {p.label}
              </a>
            ))}
          </nav>

        </div>
      </header>

      <main className="form-area">
        {error && <div className="err">{error}</div>}

        <div className="hero">
          <h1>{currentLabel}</h1>
        </div>

        {/* ── Who has paid ── */}
        {/* ── Dashboard ── */}
        {page === "dashboard" && dash && (
          <>
            <section className="card">
              <div className="card-hdr">
                <div className="card-title">Overview · {monthLabel(month)}</div>
                <MonthPicker value={month} onChange={setMonth} />
              </div>
              <div className="stats">
                <div className="stat">
                  <div className="stat-label">Rent due</div>
                  <div className="stat-value">{money(dash.totals.due)}</div>
                  <div className="stat-sub">{dash.totals.activeCount} tenants this month</div>
                </div>
                <div className="stat">
                  <div className="stat-label">Rent collected</div>
                  <div className="stat-value pos">{money(dash.totals.collected)}</div>
                  <div className="stat-sub">
                    {dash.totals.due ? Math.round((dash.totals.collected / dash.totals.due) * 100) : 0}% of rent due
                  </div>
                </div>
                <div className="stat">
                  <div className="stat-label">Paid in full</div>
                  <div className="stat-value">{dash.totals.percentFullyPaid}%</div>
                  <div className="stat-sub">
                    {dash.totals.fullyPaid} of {dash.totals.activeCount} tenants
                  </div>
                </div>
                <div className="stat">
                  <div className="stat-label">Occupancy</div>
                  <div className="stat-value">{dash.totals.occupancy}%</div>
                  <div className="stat-sub">
                    {dash.totals.occupied} of {dash.totals.totalRooms} rooms occupied
                  </div>
                </div>
              </div>
            </section>

            <div className="dash-grid">
              <section className="card">
                <div className="card-hdr">
                  <div className="card-title">Rent due this week</div>
                  <span className="muted-text">Next 7 days</span>
                </div>
                {dueSoon.length === 0 && <p className="empty">No rent due in the next 7 days.</p>}
                {dueSoon.map((d) => (
                  <div className="due-item" key={`${d.renterId}-${d.dueDate}`}>
                    <div>
                      <strong>{d.name}</strong>
                      <div className="muted-text">
                        {d.place} · due {d.dueDate}
                      </div>
                    </div>
                    <div style={{ textAlign: "right" }}>
                      <div className="num">{money(Math.max(0, d.due - d.paid))} to pay</div>
                      <span className={pillClass[d.state]}>{d.state}</span>
                    </div>
                  </div>
                ))}
              </section>

              <section className="card">
                <div className="card-hdr">
                  <div className="card-title">Occupancy by residence</div>
                </div>
                {dash.properties.length === 0 && <p className="empty">No residences yet.</p>}
                {dash.properties.map((p) => (
                  <div className="occ-item" key={p.id}>
                    <Donut value={p.occupied} total={p.rooms} color={propertyColor(p.id)} />
                    <div>
                      <strong>{p.name}</strong>
                      <div className="muted-text">
                        {p.occupied} of {p.rooms} rooms occupied
                      </div>
                      <div className="muted-text">{p.rooms - p.occupied} vacant</div>
                    </div>
                  </div>
                ))}
              </section>
            </div>

            <section className="card">
              <div className="card-hdr">
                <div className="card-title">Rent heatmap · last 12 months</div>
                <div className="legend">
                  <span><span className="dot" style={{ background: STATE_COLOR.PAID }} />Paid</span>
                  <span><span className="dot" style={{ background: STATE_COLOR.PARTIAL }} />Partly paid</span>
                  <span><span className="dot" style={{ background: STATE_COLOR["NOT PAID"] }} />Unpaid</span>
                </div>
              </div>
              <Heatmap data={heatmap} />
            </section>

            <section className="card">
              <div className="card-hdr">
                <div className="card-title">Overdue payments</div>
              </div>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Tenant</th>
                      <th>Room</th>
                      <th>Due date</th>
                      <th className="num">Days late</th>
                      <th className="num">Still owed</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {dash.overdue.length === 0 && (
                      <tr>
                        <td colSpan="6" className="empty">Nobody is overdue for this month.</td>
                      </tr>
                    )}
                    {dash.overdue.map((o) => (
                      <tr key={`${o.renterId}-${o.dueDate}`}>
                        <td>{o.name}</td>
                        <td>{o.place}</td>
                        <td>{o.dueDate}</td>
                        <td className="num">{o.daysLate}</td>
                        <td className="num neg-text">{money(o.owed)}</td>
                        <td>
                          <span className={`pill ${statePill[o.state]}`}>{o.state}</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="card">
              <div className="card-hdr">
                <div className="card-title">By property</div>
              </div>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Residence</th>
                      <th className="num">Rooms</th>
                      <th className="num">Occupied</th>
                      <th className="num">Occupancy</th>
                      <th className="num">Rent due</th>
                      <th className="num">Collected</th>
                    </tr>
                  </thead>
                  <tbody>
                    {dash.properties.map((p) => (
                      <tr key={p.id}>
                        <td>{p.name}</td>
                        <td className="num">{p.rooms}</td>
                        <td className="num">{p.occupied}</td>
                        <td className="num">{p.occupancy}%</td>
                        <td className="num">{money(p.due)}</td>
                        <td className="num">{money(p.collected)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          </>
        )}

        {/* ── Who has paid ── */}
        {page === "status" && (
          <section className="card">
            <div className="card-hdr">
              <div className="card-title">Rent status for the month</div>
              <MonthPicker value={month} onChange={setMonth} />
            </div>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Room</th>
                    <th>Tenant</th>
                    <th className="num">Rent due</th>
                    <th className="num">Paid</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {status.length === 0 && (
                    <tr>
                      <td colSpan="5" className="empty">
                        No tenancies in this month. Add them on the Properties &amp; rooms page.
                      </td>
                    </tr>
                  )}
                  {status.map((s) => (
                    <tr key={s.tenancyId} className="room-row" style={{ "--prop": propertyColor(s.propertyId) }}>
                      <td>
                        <span className="prop-dot" style={{ background: propertyColor(s.propertyId) }} />
                        <span className="prop-name" style={{ "--prop": propertyColor(s.propertyId) }}>{s.place}</span>
                      </td>
                      <td>{s.name}</td>
                      <td className="num">{money(s.monthlyRent)}</td>
                      <td className="num">{money(s.paid)}</td>
                      <td>
                        <span className={pillClass[s.state]}>{s.state}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}

        {/* ── Record payment ── */}
        {page === "payment" && (
          <section className="card">
            <div className="card-hdr">
              <div className="card-title">Record a payment</div>
            </div>
            <form onSubmit={addPayment}>
              <div className="form-grid">
                <div className="field">
                  <label>Tenant</label>
                  <select value={pf.renterId} onChange={(e) => setPf({ ...pf, renterId: e.target.value })} required>
                    <option value="">Choose tenant</option>
                    {renters.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label>For month</label>
                  <input type="month" value={pf.month} onChange={(e) => setPf({ ...pf, month: e.target.value })} required />
                </div>
                <div className="field">
                  <label>Amount paid ($)</label>
                  <MoneyInput value={pf.amount} onChange={(v) => setPf({ ...pf, amount: v })} required />
                </div>
                <div className="field">
                  <label>Date paid</label>
                  <input type="date" value={pf.paidDate} onChange={(e) => setPf({ ...pf, paidDate: e.target.value })} required />
                </div>
                <div className="field">
                  <button className="btn btn-primary">Save payment</button>
                </div>
              </div>
            </form>

            <div className="card-hdr" style={{ marginTop: 24 }}>
              <div className="card-title">All payments</div>
              <div className="inline-field">
                <label>Month</label>
                <input type="month" value={payMonth} onChange={(e) => setPayMonth(e.target.value)} />
                <button className="btn btn-outline btn-sm" onClick={() => setPayMonth("")} disabled={!payMonth}>
                  Show all months
                </button>
              </div>
            </div>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Tenant</th>
                    <th>For month</th>
                    <th className="num">Amount</th>
                    <th>Date paid</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {shownPayments.length === 0 && (
                    <tr>
                      <td colSpan="5" className="empty">No payments for this month.</td>
                    </tr>
                  )}
                  {shownPayments.map((p) => (
                    <Fragment key={p.id}>
                      <tr>
                        <td>{p.renterName}</td>
                        <td>{p.month}</td>
                        <td className="num">{money(p.amount)}</td>
                        <td>{p.paidDate}</td>
                        <td className="row-actions">
                          <button className="btn btn-outline btn-sm" onClick={() => openPaymentEditor(p)}>
                            {editingPaymentId === p.id ? "Close" : "Edit"}
                          </button>
                          <button className="btn btn-danger-ghost" onClick={() => deletePayment(p.id)}>
                            Delete
                          </button>
                        </td>
                      </tr>
                      {editingPaymentId === p.id && (
                        <tr className="edit-row">
                          <td colSpan="5">
                            <form onSubmit={savePayment}>
                              <div className="form-grid">
                                <div className="field">
                                  <label>Tenant</label>
                                  <select
                                    value={paymentEdit.renterId}
                                    onChange={(e) => setPaymentEdit({ ...paymentEdit, renterId: e.target.value })}
                                    required
                                  >
                                    {renters.map((r) => (
                                      <option key={r.id} value={r.id}>
                                        {r.name}
                                      </option>
                                    ))}
                                  </select>
                                </div>
                                <div className="field">
                                  <label>For month</label>
                                  <input
                                    type="month"
                                    value={paymentEdit.month}
                                    onChange={(e) => setPaymentEdit({ ...paymentEdit, month: e.target.value })}
                                    required
                                  />
                                </div>
                                <div className="field">
                                  <label>Amount paid ($)</label>
                                  <MoneyInput
                                    value={paymentEdit.amount}
                                    onChange={(v) => setPaymentEdit({ ...paymentEdit, amount: v })}
                                    required
                                  />
                                </div>
                                <div className="field">
                                  <label>Date paid</label>
                                  <input
                                    type="date"
                                    value={paymentEdit.paidDate}
                                    onChange={(e) => setPaymentEdit({ ...paymentEdit, paidDate: e.target.value })}
                                    required
                                  />
                                </div>
                                <div className="field">
                                  <button className="btn btn-primary">Save changes</button>
                                </div>
                              </div>
                            </form>
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  ))}
                  {shownPayments.length > 0 && (
                    <tr className="total">
                      <th>Total ({shownPayments.length} payments)</th>
                      <th></th>
                      <th className="num">{money(shownTotal)}</th>
                      <th></th>
                      <th></th>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>
        )}

        {/* ── Tenant statement ── */}
        {page === "statement" && (
          <section className="card statement">
            <div className="card-hdr no-print">
              <div className="card-title">Tenant statement</div>
              <button className="btn btn-primary" onClick={() => window.print()} disabled={!statement}>
                Print statement
              </button>
            </div>

            <div className="range-row no-print">
              <div className="field">
                <label>Tenant</label>
                <select value={stmtRenter} onChange={(e) => setStmtRenter(e.target.value)}>
                  <option value="">Choose tenant</option>
                  {renters.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label>From</label>
                <input type="date" value={stmtRange.from} onChange={(e) => setStmtRange({ ...stmtRange, from: e.target.value })} />
              </div>
              <div className="field">
                <label>To</label>
                <input type="date" value={stmtRange.to} onChange={(e) => setStmtRange({ ...stmtRange, to: e.target.value })} />
              </div>
              <div className="presets">
                {rangePresets().map((p) => (
                  <button key={p.label} className="preset" onClick={() => setStmtRange(p.range)}>
                    {p.label}
                  </button>
                ))}
              </div>
            </div>

            {!statement && <p className="empty">Choose a tenant and dates to see their statement.</p>}

            {statement && (
              <div className="statement-body">
                <div className="stmt-head">
                  <div>
                    <div className="stmt-title">{statement.renter.name}</div>
                    <div className="muted-text">Date of birth {statement.renter.dob}</div>
                  </div>
                  <div className="stmt-meta">
                    <div>Period: {statement.from} to {statement.to}</div>
                    {statement.movedIn && <div>Moved in: {statement.movedIn}</div>}
                    <div>Issued: {statement.generatedOn}</div>
                  </div>
                </div>

                <h3 className="stmt-h">Tenancies</h3>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Room</th>
                        <th>Moved in</th>
                        <th>Moved out</th>
                        <th className="num">Monthly rent</th>
                        <th className="num">Due day</th>
                      </tr>
                    </thead>
                    <tbody>
                      {statement.tenancies.length === 0 && (
                        <tr>
                          <td colSpan="5" className="empty">No tenancies in these dates.</td>
                        </tr>
                      )}
                      {statement.tenancies.map((t, i) => (
                        <tr key={i}>
                          <td>{t.place}</td>
                          <td>{t.startDate}</td>
                          <td>{t.endDate || "Current"}</td>
                          <td className="num">{money(t.monthlyRent)}</td>
                          <td className="num">{t.dueDay}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <h3 className="stmt-h">Payments received</h3>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>For month</th>
                        <th>Date paid</th>
                        <th className="num">Amount</th>
                      </tr>
                    </thead>
                    <tbody>
                      {statement.payments.length === 0 && (
                        <tr>
                          <td colSpan="3" className="empty">No payments in these dates.</td>
                        </tr>
                      )}
                      {statement.payments.map((p, i) => (
                        <tr key={i}>
                          <td>{monthLabel(p.month)}</td>
                          <td>{p.paidDate}</td>
                          <td className="num">{money(p.amount)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <p className="muted-text note">
                  Rent due is counted for each calendar month a tenancy covers. Amounts are in dollars ($).
                </p>
              </div>
            )}
          </section>
        )}

        {/* ── Properties & rooms ── */}
        {page === "places" && (
          <>
            <section className="card">
              <div className="card-hdr">
                <div className="card-title">Properties</div>
              </div>
              <form onSubmit={addProperty}>
                <div className="form-grid">
                  <div className="field">
                    <label>Property name</label>
                    <input
                      placeholder="e.g. 12 Smith St"
                      value={propForm.name}
                      onChange={(e) => setPropForm({ ...propForm, name: e.target.value })}
                      required
                    />
                  </div>
                  <div className="field">
                    <label>Address (optional)</label>
                    <input value={propForm.address} onChange={(e) => setPropForm({ ...propForm, address: e.target.value })} />
                  </div>
                  <div className="field">
                    <button className="btn btn-primary">Add property</button>
                  </div>
                </div>
              </form>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Address</th>
                      <th className="num">Rooms</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {properties.length === 0 && (
                      <tr>
                        <td colSpan="4" className="empty">No properties yet.</td>
                      </tr>
                    )}
                    {properties.map((p) => (
                      <tr key={p.id}>
                        <td>
                          <span className="prop-dot" style={{ background: propertyColor(p.id) }} />
                          <span className="prop-name" style={{ "--prop": propertyColor(p.id) }}>{p.name}</span>
                        </td>
                        <td>{p.address}</td>
                        <td className="num">{p.roomCount}</td>
                        <td>
                          <button className="btn btn-danger-ghost" onClick={() => deleteProperty(p.id)}>
                            Delete
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="card">
              <div className="card-hdr">
                <div className="card-title">Room map</div>
                <div className="legend">
                  {Object.entries(ROOM_STATE).map(([key, s]) => (
                    <span key={key}>
                      <span className="dot" style={{ background: s.bg }} />
                      {s.label}
                    </span>
                  ))}
                </div>
              </div>
              {properties.length === 0 && <p className="empty">Add a residence first.</p>}
              {properties.map((p) => {
                const list = rooms.filter((r) => r.propertyId === p.id);
                return (
                  <div className="room-map-group" key={p.id}>
                    <div className="room-map-title">
                      <span className="prop-dot" style={{ background: propertyColor(p.id) }} />
                      {p.name}
                      <span className="muted-text">
                        {list.filter((r) => r.tenancyId).length} of {list.length} occupied · click a room to update it
                      </span>
                    </div>
                    <div className="room-map">
                      {list.map((r) => {
                        const s = ROOM_STATE[roomKey(r)];
                        return (
                          <button
                            key={r.id}
                            type="button"
                            className="room-tile"
                            style={{ background: s.bg }}
                            onClick={() => {
                              openRoomEditor(r);
                              setTimeout(
                                () => document.getElementById(`room-row-${r.id}`)?.scrollIntoView({ behavior: "smooth", block: "center" }),
                                80
                              );
                            }}
                          >
                            <strong>{r.name}</strong>
                            <small>{r.tenant ?? "No tenant"}</small>
                            <small>{s.label}</small>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                );
              })}
            </section>

            <section className="card">
              <div className="card-hdr">
                <div className="card-title">Rooms</div>
              </div>
              <form onSubmit={addRoom}>
                <div className="form-grid">
                  <div className="field">
                    <label>Property</label>
                    <select
                      value={roomForm.propertyId}
                      onChange={(e) => setRoomForm({ ...roomForm, propertyId: e.target.value })}
                      required
                    >
                      <option value="">Choose property</option>
                      {properties.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="field">
                    <label>Room name or number</label>
                    <input
                      placeholder="e.g. Room 1"
                      value={roomForm.name}
                      onChange={(e) => setRoomForm({ ...roomForm, name: e.target.value })}
                      required
                    />
                  </div>
                  <div className="field">
                    <label>Rental price ($, optional)</label>
                    <MoneyInput value={roomForm.askingRent} onChange={(v) => setRoomForm({ ...roomForm, askingRent: v })} />
                  </div>
                  <div className="field">
                    <button className="btn btn-primary">Add room</button>
                  </div>
                </div>
              </form>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Property</th>
                      <th>Room</th>
                      <th>Current tenant</th>
                      <th>Moves in</th>
                      <th className="num">Monthly rent</th>
                      <th>Due date</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {rooms.length === 0 && (
                      <tr>
                        <td colSpan="7" className="empty">No rooms yet.</td>
                      </tr>
                    )}
                    {rooms.map((r) => (
                      <Fragment key={r.id}>
                        <tr id={`room-row-${r.id}`} className="room-row" style={{ "--prop": propertyColor(r.propertyId) }}>
                          <td>
                            <span className="prop-dot" style={{ background: propertyColor(r.propertyId) }} />
                            <span className="prop-name">{r.propertyName}</span>
                          </td>
                          <td>{r.name}</td>
                          <td>
                            {r.tenant ?? <span className="muted">Vacant</span>}
                            {r.tenantEnds && <div className="muted-text">Ends {r.tenantEnds}</div>}
                          </td>
                          <td>{r.movedIn || "–"}</td>
                          <td className="num">{r.rent != null ? money(r.rent) : "–"}</td>
                          <td>{r.dueDay ? `Every month, day ${r.dueDay}` : "–"}</td>
                          <td className="row-actions">
                            <button className="btn btn-outline btn-sm" onClick={() => openRoomEditor(r)}>
                              {editingRoomId === r.id ? "Close" : "Update"}
                            </button>
                            <button className="btn btn-danger-ghost" onClick={() => deleteRoom(r.id)}>
                              Delete
                            </button>
                          </td>
                        </tr>
                        {editingRoomId === r.id && (
                          <tr className="edit-row">
                            <td colSpan="7">
                              <div className="room-edit-title">
                                {r.name} · {r.propertyName}
                              </div>
                              {r.tenancyId ? (
                                <>
                                  <div className="room-edit-now">
                                    <span>Now living here: <strong>{r.tenant}</strong></span>
                                    <label className="inline-field">
                                      End on
                                      <input
                                        type="date"
                                        value={roomAssign.endDate}
                                        onChange={(e) => setRoomAssign({ ...roomAssign, endDate: e.target.value })}
                                      />
                                    </label>
                                    <button className="btn btn-end" onClick={() => endRoomTenancy(r.tenancyId)}>
                                      End tenancy
                                    </button>
                                    <span className="muted-text">End the current tenancy before assigning someone new.</span>
                                  </div>

                                  <form onSubmit={(e) => saveRoomTerms(r.tenancyId, e)} className="room-subform">
                                    <div className="room-edit-title">Current tenancy</div>
                                    <div className="form-grid">
                                      <div className="field">
                                        <label>Moves in</label>
                                        <input
                                          type="date"
                                          value={roomTerms.startDate}
                                          onChange={(e) => setRoomTerms({ ...roomTerms, startDate: e.target.value })}
                                          required
                                        />
                                      </div>
                                      <div className="field">
                                        <label>Monthly rent ($)</label>
                                        <MoneyInput
                                          value={roomTerms.monthlyRent}
                                          onChange={(v) => setRoomTerms({ ...roomTerms, monthlyRent: v })}
                                          required
                                        />
                                      </div>
                                      <div className="field">
                                        <label>Rent due day (1–28)</label>
                                        <input
                                          type="number"
                                          min="1"
                                          max="28"
                                          value={roomTerms.dueDay}
                                          onChange={(e) => setRoomTerms({ ...roomTerms, dueDay: e.target.value })}
                                          required
                                        />
                                      </div>
                                      <div className="field">
                                        <button className="btn btn-primary">Save changes</button>
                                      </div>
                                    </div>
                                  </form>

                                  <form onSubmit={(e) => changeRoomTenant(r.tenancyId, e)} className="room-subform">
                                    <div className="room-edit-title">Change tenant</div>
                                    <p className="muted-text">
                                      The current tenant's tenancy ends on the new move-in date, and the new tenant starts that day.
                                    </p>
                                    <div className="form-grid">
                                      <div className="field">
                                        <label>New tenant</label>
                                        <select
                                          value={roomSwap.renterId}
                                          onChange={(e) => setRoomSwap({ ...roomSwap, renterId: e.target.value })}
                                          required
                                        >
                                          <option value="">Choose tenant</option>
                                          {renters.filter((rt) => !rt.room).map((rt) => (
                                            <option key={rt.id} value={rt.id}>
                                              {rt.name}
                                            </option>
                                          ))}
                                        </select>
                                      </div>
                                      <div className="field">
                                        <label>Moves in</label>
                                        <input
                                          type="date"
                                          value={roomSwap.startDate}
                                          onChange={(e) => setRoomSwap({ ...roomSwap, startDate: e.target.value })}
                                          required
                                        />
                                      </div>
                                      <div className="field">
                                        <label>Monthly rent ($)</label>
                                        <MoneyInput
                                          value={roomSwap.monthlyRent}
                                          onChange={(v) => setRoomSwap({ ...roomSwap, monthlyRent: v })}
                                          required
                                        />
                                      </div>
                                      <div className="field">
                                        <label>Rent due day (1–28)</label>
                                        <input
                                          type="number"
                                          min="1"
                                          max="28"
                                          value={roomSwap.dueDay}
                                          onChange={(e) => setRoomSwap({ ...roomSwap, dueDay: e.target.value })}
                                          required
                                        />
                                      </div>
                                      <div className="field">
                                        <button className="btn btn-primary">Change tenant</button>
                                      </div>
                                    </div>
                                  </form>
                                </>
                              ) : (
                                <p className="muted-text">This room is vacant. Assign a tenant below.</p>
                              )}
                              {!r.tenancyId && (
                              <form onSubmit={assignRoomRenter}>
                                <div className="form-grid">
                                  <div className="field">
                                    <label>Tenant</label>
                                    <select
                                      value={roomAssign.renterId}
                                      onChange={(e) => setRoomAssign({ ...roomAssign, renterId: e.target.value })}
                                      required
                                    >
                                      <option value="">Choose tenant</option>
                                      {renters.filter((rt) => !rt.room).map((rt) => (
                                        <option key={rt.id} value={rt.id}>
                                          {rt.name}
                                        </option>
                                      ))}
                                    </select>
                                  </div>
                                  <div className="field">
                                    <label>Moves in</label>
                                    <input
                                      type="date"
                                      value={roomAssign.startDate}
                                      onChange={(e) => setRoomAssign({ ...roomAssign, startDate: e.target.value })}
                                      required
                                    />
                                  </div>
                                  <div className="field">
                                    <label>Monthly rent ($)</label>
                                    <MoneyInput
                                      value={roomAssign.monthlyRent}
                                      onChange={(v) => setRoomAssign({ ...roomAssign, monthlyRent: v })}
                                      required
                                    />
                                  </div>
                                  <div className="field">
                                    <label>Rent due day (1–28)</label>
                                    <input
                                      type="number"
                                      min="1"
                                      max="28"
                                      value={roomAssign.dueDay}
                                      onChange={(e) => setRoomAssign({ ...roomAssign, dueDay: e.target.value })}
                                      required
                                    />
                                  </div>
                                  <div className="field">
                                    <button className="btn btn-primary" disabled={!!r.tenancyId}>
                                      Assign tenant
                                    </button>
                                  </div>
                                </div>
                              </form>
                              )}
                            </td>
                          </tr>
                        )}
                      </Fragment>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

          </>
        )}

        {/* ── Add renter ── */}
        {page === "renters" && (
          <section className="card">
            <div className="card-hdr">
              <div className="card-title">Add tenant</div>
            </div>
            <form onSubmit={addRenter}>
              <div className="form-grid">
                <div className="field">
                  <label>Name</label>
                  <input
                    placeholder="Tenant name"
                    value={rf.name}
                    onChange={(e) => setRf({ ...rf, name: e.target.value })}
                    required
                  />
                </div>
                <div className="field">
                  <label>Date of birth</label>
                  <input type="date" value={rf.dob} onChange={(e) => setRf({ ...rf, dob: e.target.value })} required />
                </div>
                <div className="field">
                  <label>ID photo (JPG or PNG, optional)</label>
                  <input key={photoKey} type="file" accept="image/jpeg,image/png" onChange={choosePhoto} />
                </div>
                <div className="field">
                  <button className="btn btn-primary">Add tenant</button>
                </div>
              </div>
            </form>

            <div className="card-hdr" style={{ marginTop: 24 }}>
              <div className="card-title">All tenants</div>
              <div className="inline-field">
                <label>Status</label>
                <select value={renterFilter} onChange={(e) => setRenterFilter(e.target.value)} style={{ width: 180 }}>
                  <option value="ALL">All</option>
                  <option value="CURRENT">Current tenant</option>
                  <option value="MOVED_OUT">Moved out</option>
                </select>
              </div>
            </div>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Date of birth</th>
                    <th>Status</th>
                    <th>Room</th>
                    <th>ID photo</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {renters.filter((r) => renterFilter === "ALL" || r.status === renterFilter).length === 0 && (
                    <tr>
                      <td colSpan="6" className="empty">No tenants match this filter.</td>
                    </tr>
                  )}
                  {renters
                    .filter((r) => renterFilter === "ALL" || r.status === renterFilter)
                    .map((r) => (
                      <tr key={r.id}>
                        <td>{r.name}</td>
                        <td>{r.dob}</td>
                        <td>
                          <span className={`pill ${r.status === "CURRENT" ? "pill-paid" : "pill-muted"}`}>
                            {RENTER_STATUS[r.status]}
                          </span>
                        </td>
                        <td>{r.room ?? <span className="muted">Not assigned</span>}</td>
                        <td>
                          {r.hasPhoto ? (
                            <>
                              <a href={`/renters/${r.id}/photo`} target="_blank" rel="noreferrer">
                                View ID
                              </a>{" "}
                              <button className="btn btn-outline btn-sm" onClick={() => removePhoto(r.id)}>
                                Remove
                              </button>
                            </>
                          ) : (
                            <label className="btn btn-outline btn-sm file-btn">
                              Upload ID
                              <input
                                type="file"
                                accept="image/jpeg,image/png"
                                hidden
                                onChange={(e) => {
                                  const f = e.target.files[0];
                                  e.target.value = "";
                                  if (f) uploadPhoto(r.id, f);
                                }}
                              />
                            </label>
                          )}
                        </td>
                        <td className="row-actions">
                          <button className="btn btn-outline btn-sm" onClick={() => openRenterEditor(r)}>
                            Edit
                          </button>
                          <button className="btn btn-danger-ghost" onClick={() => deleteRenter(r.id)}>
                            Delete
                          </button>
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>

            {renterEdit && (
              <Modal title={`Edit tenant · ${renterEdit.name}`} onClose={() => setRenterEdit(null)}>
                {renterError && <div className="err">{renterError}</div>}
                <form onSubmit={saveRenterEdit}>
                  <div className="form-grid">
                    <div className="field">
                      <label>Name</label>
                      <input
                        value={renterEdit.name}
                        onChange={(e) => setRenterEdit({ ...renterEdit, name: e.target.value })}
                        required
                      />
                    </div>
                    <div className="field">
                      <label>Date of birth</label>
                      <input
                        type="date"
                        value={renterEdit.dob}
                        onChange={(e) => setRenterEdit({ ...renterEdit, dob: e.target.value })}
                        required
                      />
                    </div>
                    <div className="field">
                      <label>Status</label>
                      <select value={renterEdit.status} onChange={(e) => setRenterEdit({ ...renterEdit, status: e.target.value })}>
                        <option value="CURRENT">Current tenant</option>
                        <option value="MOVED_OUT">Moved out</option>
                      </select>
                    </div>
                    <div className="field">
                      <button className="btn btn-primary">Save changes</button>
                    </div>
                  </div>
                </form>
                <p className="muted-text note">To change the room, use Update on Properties &amp; rooms.</p>
              </Modal>
            )}
          </section>
        )}

        {/* ── Expenses ── */}
        {page === "expenses" && (
          <section className="card">
            <div className="card-hdr">
              <div className="card-title">Add an expense</div>
              <YearPicker value={fyYear} onChange={setFyYear} />
            </div>
            <form onSubmit={addExpense}>
              <div className="form-grid">
                <div className="field">
                  <label>Property</label>
                  <select
                    value={expForm.propertyId}
                    onChange={(e) => setExpForm({ ...expForm, propertyId: e.target.value })}
                    required
                  >
                    <option value="">Choose property</option>
                    {properties.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label>Date paid</label>
                  <input type="date" value={expForm.date} onChange={(e) => setExpForm({ ...expForm, date: e.target.value })} required />
                </div>
                <div className="field">
                  <label>Category</label>
                  <select value={expForm.category} onChange={(e) => setExpForm({ ...expForm, category: e.target.value })} required>
                    <option value="">Choose category</option>
                    {categories.map((c) => (
                      <option key={c} value={c}>
                        {c}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label>Amount ($)</label>
                  <MoneyInput value={expForm.amount} onChange={(v) => setExpForm({ ...expForm, amount: v })} required />
                </div>
                <div className="field">
                  <label>Note (optional)</label>
                  <input value={expForm.note} onChange={(e) => setExpForm({ ...expForm, note: e.target.value })} />
                </div>
                <div className="field">
                  <button className="btn btn-primary">Add expense</button>
                </div>
              </div>
            </form>

            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Property</th>
                    <th>Category</th>
                    <th>Note</th>
                    <th className="num">Amount</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {expenses.length === 0 && (
                    <tr>
                      <td colSpan="6" className="empty">No expenses in {fyLabel(fyYear)}.</td>
                    </tr>
                  )}
                  {expenses.map((e) => (
                    <tr key={e.id}>
                      <td>{e.date}</td>
                      <td>{e.propertyName}</td>
                      <td>{e.category}</td>
                      <td>{e.note}</td>
                      <td className="num">{money(e.amount)}</td>
                      <td>
                        <button className="btn btn-danger-ghost" onClick={() => deleteExpense(e.id)}>
                          Delete
                        </button>
                      </td>
                    </tr>
                  ))}
                  {expenses.length > 0 && (
                    <tr className="total">
                      <th colSpan="4">Total for {fyLabel(fyYear)}</th>
                      <th className="num">{money(expenseTotal)}</th>
                      <th></th>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>
        )}

        {/* ── Report ── */}
        {page === "report" && (
          <>
            <section className="card">
              <div className="card-hdr">
                <div className="tabs">
                  <button className={`tab${reportTab === "profit" ? " active" : ""}`} onClick={() => setReportTab("profit")}>
                    Profit
                  </button>
                  <button className={`tab${reportTab === "monthly" ? " active" : ""}`} onClick={() => setReportTab("monthly")}>
                    Monthly income
                  </button>
                  <button className={`tab${reportTab === "expenses" ? " active" : ""}`} onClick={() => setReportTab("expenses")}>
                    Expenses
                  </button>
                </div>
                <a className="btn btn-outline" href={`/reports/${reportTab}.csv?from=${range.from}&to=${range.to}`}>
                  Download CSV
                </a>
              </div>

              <div className="range-row">
                <div className="field">
                  <label>From</label>
                  <input type="date" value={range.from} onChange={(e) => setRange({ ...range, from: e.target.value })} />
                </div>
                <div className="field">
                  <label>To</label>
                  <input type="date" value={range.to} onChange={(e) => setRange({ ...range, to: e.target.value })} />
                </div>
              </div>
            </section>

            {reportTab === "monthly" && monthlyReport && (
              <>
                <section className="card">
                  <div className="card-hdr">
                    <div className="card-title">
                      Monthly income · {monthlyReport.from} to {monthlyReport.to}
                    </div>
                  </div>
                  <div className="stats">
                    {[
                      ["Rent collected", monthlyReport.totals.collected],
                      ["Expenses", monthlyReport.totals.expenses],
                      ["Profit", monthlyReport.totals.profit],
                    ].map(([label, value]) => (
                      <div className="stat" key={label}>
                        <div className="stat-label">{label}</div>
                        <div className={`stat-value${label === "Profit" ? (value < 0 ? " neg" : " pos") : ""}`}>
                          {money(value)}
                        </div>
                      </div>
                    ))}
                  </div>
                </section>

                <section className="card">
                  <div className="card-hdr">
                    <div className="card-title">Month by month</div>
                  </div>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Month</th>
                          <th className="num">Rent due</th>
                          <th className="num">Rent collected</th>
                          <th className="num">Still owed</th>
                          <th className="num">Expenses</th>
                          <th className="num">Profit</th>
                        </tr>
                      </thead>
                      <tbody>
                        {monthlyReport.months.length === 0 && (
                          <tr>
                            <td colSpan="6" className="empty">Nothing recorded in these dates.</td>
                          </tr>
                        )}
                        {monthlyReport.months.map((m) => (
                          <tr key={m.month}>
                            <td>{monthLabel(m.month)}</td>
                            <td className="num">{money(m.charged)}</td>
                            <td className="num">{money(m.collected)}</td>
                            <td className="num">{money(m.arrears)}</td>
                            <td className="num">{money(m.expenses)}</td>
                            <td className={`num ${m.profit < 0 ? "neg-text" : "pos-text"}`}>{money(m.profit)}</td>
                          </tr>
                        ))}
                        {monthlyReport.months.length > 0 && (
                          <tr className="total">
                            <th>Total</th>
                            <th className="num">{money(monthlyReport.totals.charged)}</th>
                            <th className="num">{money(monthlyReport.totals.collected)}</th>
                            <th className="num">{money(monthlyReport.totals.arrears)}</th>
                            <th className="num">{money(monthlyReport.totals.expenses)}</th>
                            <th className="num">{money(monthlyReport.totals.profit)}</th>
                          </tr>
                        )}
                      </tbody>
                    </table>
                  </div>
                  <p className="muted-text note">
                    Income is the rent collected in each month. Expenses are counted by the date they were paid.
                  </p>
                </section>
              </>
            )}

            {reportTab === "profit" && profitReport && (
              <>
                <section className="card">
                  <div className="card-hdr">
                    <div className="card-title">
                      Profit · {profitReport.from} to {profitReport.to}
                    </div>
                  </div>
                  <div className="stats">
                    {[
                      ["Rent charged", profitReport.totals.charged],
                      ["Rent collected", profitReport.totals.collected],
                      ["Arrears", profitReport.totals.arrears],
                      ["Expenses", profitReport.totals.expenses],
                      ["Profit", profitReport.totals.profit],
                    ].map(([label, value]) => (
                      <div className="stat" key={label}>
                        <div className="stat-label">{label}</div>
                        <div className={`stat-value${label === "Profit" ? (value < 0 ? " neg" : " pos") : ""}`}>
                          {money(value)}
                        </div>
                      </div>
                    ))}
                  </div>
                  <p className="muted-text note">
                    Profit is rent collected minus expenses. Rent is counted by calendar month, so a tenancy counts for
                    every month it covers within the dates you chose.
                  </p>
                </section>

                <section className="card">
                  <div className="card-hdr">
                    <div className="card-title">Profit by property</div>
                  </div>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Property</th>
                          <th className="num">Rent charged</th>
                          <th className="num">Rent collected</th>
                          <th className="num">Arrears</th>
                          <th className="num">Expenses</th>
                          <th className="num">Profit</th>
                        </tr>
                      </thead>
                      <tbody>
                        {profitReport.properties.length === 0 && (
                          <tr>
                            <td colSpan="6" className="empty">Nothing recorded in these dates.</td>
                          </tr>
                        )}
                        {profitReport.properties.map((p) => (
                          <tr key={p.id}>
                            <td>{p.name}</td>
                            <td className="num">{money(p.charged)}</td>
                            <td className="num">{money(p.collected)}</td>
                            <td className="num">{money(p.arrears)}</td>
                            <td className="num">{money(p.expenses)}</td>
                            <td className={`num ${p.profit < 0 ? "neg-text" : "pos-text"}`}>{money(p.profit)}</td>
                          </tr>
                        ))}
                        {profitReport.properties.length > 0 && (
                          <tr className="total">
                            <th>Total</th>
                            <th className="num">{money(profitReport.totals.charged)}</th>
                            <th className="num">{money(profitReport.totals.collected)}</th>
                            <th className="num">{money(profitReport.totals.arrears)}</th>
                            <th className="num">{money(profitReport.totals.expenses)}</th>
                            <th className="num">{money(profitReport.totals.profit)}</th>
                          </tr>
                        )}
                      </tbody>
                    </table>
                  </div>
                  <p className="muted-text note">
                    This is a working summary to share with your accountant, not a tax return. Depreciation, capital
                    works, and other adjustments need to be checked with your accountant.
                  </p>
                </section>
              </>
            )}

            {reportTab === "expenses" && expensesReport && (
              <>
                <section className="card">
                  <div className="card-hdr">
                    <div className="card-title">
                      Expenses · {expensesReport.from} to {expensesReport.to}
                    </div>
                  </div>
                  <div className="stats">
                    <div className="stat">
                      <div className="stat-label">Total expenses</div>
                      <div className="stat-value">{money(expensesReport.total)}</div>
                    </div>
                    <div className="stat">
                      <div className="stat-label">Number of expenses</div>
                      <div className="stat-value">{expensesReport.items.length}</div>
                    </div>
                  </div>
                </section>

                <section className="card">
                  <div className="card-hdr">
                    <div className="card-title">By category</div>
                  </div>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Category</th>
                          <th className="num">Amount</th>
                        </tr>
                      </thead>
                      <tbody>
                        {expensesReport.byCategory.length === 0 && (
                          <tr>
                            <td colSpan="2" className="empty">No expenses in these dates.</td>
                          </tr>
                        )}
                        {expensesReport.byCategory.map((c) => (
                          <tr key={c.category}>
                            <td>{c.category}</td>
                            <td className="num">{money(c.amount)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </section>

                <section className="card">
                  <div className="card-hdr">
                    <div className="card-title">By property</div>
                  </div>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Property</th>
                          <th className="num">Total</th>
                        </tr>
                      </thead>
                      <tbody>
                        {expensesReport.byProperty.length === 0 && (
                          <tr>
                            <td colSpan="2" className="empty">No expenses in these dates.</td>
                          </tr>
                        )}
                        {expensesReport.byProperty.map((p) => (
                          <tr key={p.id}>
                            <td>{p.name}</td>
                            <td className="num">{money(p.total)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </section>

                <section className="card">
                  <div className="card-hdr">
                    <div className="card-title">All expenses</div>
                  </div>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Date</th>
                          <th>Property</th>
                          <th>Category</th>
                          <th>Note</th>
                          <th className="num">Amount</th>
                        </tr>
                      </thead>
                      <tbody>
                        {expensesReport.items.length === 0 && (
                          <tr>
                            <td colSpan="5" className="empty">No expenses in these dates.</td>
                          </tr>
                        )}
                        {expensesReport.items.map((i) => (
                          <tr key={i.id}>
                            <td>{i.date}</td>
                            <td>{i.propertyName}</td>
                            <td>{i.category}</td>
                            <td>{i.note}</td>
                            <td className="num">{money(i.amount)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </section>
              </>
            )}
          </>
        )}
      </main>
    </>
  );
}
