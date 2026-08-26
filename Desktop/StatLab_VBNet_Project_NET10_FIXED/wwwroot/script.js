const state = {
  test: "sign"
};

// Use the same origin when the app is served by the VB.NET server.
// If index.html is opened directly from the file system, fall back to the
// local VB.NET API so the dashboard can still communicate with the backend.
const API_BASE = window.location.protocol === "file:" ? "http://localhost:5000" : "";

const config = {
  sign: {
    title: "Sign Test",
    pill: "Median",
    description: "Tests whether the population median differs from a hypothesized median.",
    sample: "45, 51, 62, 48, 55, 59, 42, 67, 53, 49, 61, 57"
  },
  "signed-rank": {
    title: "Wilcoxon Signed-Rank Test",
    pill: "Ranked differences",
    description: "Tests whether the distribution of paired differences is centered around a hypothesized median.",
    sample: "52, 48, 61, 57, 55, 64, 46, 58, 62, 51, 59, 66"
  },
  "kruskal-wallis": {
    title: "Kruskal-Wallis H Test",
    pill: "Independent groups",
    description: "Tests whether two or more independent groups come from the same distribution.",
    sample: ""
  }
};

const tabs = document.querySelectorAll(".test-tab");
const testTitle = document.getElementById("testTitle");
const methodPill = document.getElementById("methodPill");
const description = document.getElementById("description");
const values = document.getElementById("values");
const median = document.getElementById("median");
const alpha = document.getElementById("alpha");
const singleInputArea = document.getElementById("singleInputArea");
const groupInputArea = document.getElementById("groupInputArea");
const analyzeBtn = document.getElementById("analyzeBtn");
const sampleBtn = document.getElementById("sampleBtn");
const clearBtn = document.getElementById("clearBtn");
const addGroupBtn = document.getElementById("addGroup");
const errorBox = document.getElementById("errorBox");
const emptyResult = document.getElementById("emptyResult");
const resultContent = document.getElementById("resultContent");
const statGrid = document.getElementById("statGrid");
const decisionBadge = document.getElementById("decisionBadge");
const decisionCard = document.getElementById("decisionCard");
const conclusion = document.getElementById("conclusion");
const groupTableSection = document.getElementById("groupTableSection");
const groupTableBody = document.getElementById("groupTableBody");
const backendStatus = document.getElementById("backendStatus");

function parseNumbers(text) {
  return text
    .split(/[\s,;]+/)
    .map(v => v.trim())
    .filter(Boolean)
    .map(Number)
    .filter(v => Number.isFinite(v));
}

function setTest(test) {
  state.test = test;

  tabs.forEach(tab => tab.classList.toggle("active", tab.dataset.test === test));

  const c = config[test];
  testTitle.textContent = c.title;
  methodPill.textContent = c.pill;
  description.textContent = c.description;

  const isKruskal = test === "kruskal-wallis";
  singleInputArea.classList.toggle("hidden", isKruskal);
  groupInputArea.classList.toggle("hidden", !isKruskal);

  sampleBtn.textContent = isKruskal ? "Load sample" : "Load sample";

  clearResults();
  clearError();
}

tabs.forEach(tab => {
  tab.addEventListener("click", () => setTest(tab.dataset.test));
});

sampleBtn.addEventListener("click", () => {
  if (state.test === "kruskal-wallis") {
    const samples = [
      "12, 15, 13, 14, 16",
      "19, 17, 20, 18, 21",
      "10, 11, 9, 12, 8"
    ];

    const boxes = document.querySelectorAll(".group-box");
    boxes.forEach((box, i) => {
      if (samples[i]) box.value = samples[i];
    });
  } else {
    values.value = config[state.test].sample;
  }
});

clearBtn.addEventListener("click", () => {
  values.value = "";
  document.querySelectorAll(".group-box").forEach(box => box.value = "");
  clearResults();
  clearError();
});

addGroupBtn.addEventListener("click", () => {
  const wrapper = document.createElement("label");
  wrapper.innerHTML = `
    Group ${document.querySelectorAll(".group-box").length + 1}
    <textarea class="group-box" placeholder="Enter observations"></textarea>
  `;
  groupInputArea.insertBefore(wrapper, addGroupBtn);
});

analyzeBtn.addEventListener("click", async () => {
  clearError();

  let payload = {
    test: state.test,
    alpha: Number(alpha.value),
    median: Number(median.value)
  };

  if (state.test === "kruskal-wallis") {
    payload.groups = [...document.querySelectorAll(".group-box")]
      .map(box => parseNumbers(box.value))
      .filter(group => group.length > 0);

    if (payload.groups.length < 2) {
      showError("Please enter at least two groups.");
      return;
    }
  } else {
    payload.values = parseNumbers(values.value);

    if (payload.values.length < 1) {
      showError("Please enter at least one numeric observation.");
      return;
    }
  }

  analyzeBtn.disabled = true;
  analyzeBtn.innerHTML = "Calculating...";

  try {
    const response = await fetch(`${API_BASE}/api/analyze`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    const data = await response.json();

    if (!response.ok) {
      throw new Error(data.error || "The backend could not complete the analysis.");
    }

    renderResult(data);
  } catch (error) {
    showError(error.message);
  } finally {
    analyzeBtn.disabled = false;
    analyzeBtn.innerHTML = 'Run Analysis <span>→</span>';
  }
});

function renderResult(data) {
  emptyResult.classList.add("hidden");
  resultContent.classList.remove("hidden");

  const items = [];

  if (data.test === "Sign Test") {
    items.push(["n", data.n]);
    items.push(["Positive", data.positive]);
    items.push(["Negative", data.negative]);
    items.push(["Ties", data.ties]);
    items.push(["Statistic", data.statistic]);
    items.push(["p-value", formatNumber(data.pValue)]);
  }

  if (data.test === "Wilcoxon Signed-Rank Test") {
    items.push(["n", data.n]);
    items.push(["W⁺", formatNumber(data.wPlus)]);
    items.push(["W⁻", formatNumber(data.wMinus)]);
    items.push(["Statistic", formatNumber(data.statistic)]);
    items.push(["z", formatNumber(data.z)]);
    items.push(["p-value", formatNumber(data.pValue)]);
  }

  if (data.test === "Kruskal-Wallis H Test") {
    items.push(["Groups", data.groups]);
    items.push(["Total n", data.n]);
    items.push(["H statistic", formatNumber(data.statistic)]);
    items.push(["df", data.degreesOfFreedom]);
    items.push(["p-value", formatNumber(data.pValue)]);
  }

  statGrid.innerHTML = items.map(([label, value]) => `
    <div class="stat-card">
      <small>${label}</small>
      <strong>${value}</strong>
    </div>
  `).join("");

  const rejected = data.decision.toLowerCase().includes("reject");
  decisionBadge.textContent = data.decision;
  decisionBadge.className = `decision-badge ${rejected ? "reject" : "accept"}`;

  decisionCard.textContent = `${data.decision} at α = ${data.alpha}.`;
  conclusion.textContent = data.conclusion;

  if (data.groupSummaries) {
    groupTableSection.classList.remove("hidden");
    groupTableBody.innerHTML = data.groupSummaries.map(g => `
      <tr>
        <td>${g.group}</td>
        <td>${g.n}</td>
        <td>${formatNumber(g.rankSum)}</td>
        <td>${formatNumber(g.mean)}</td>
      </tr>
    `).join("");
  } else {
    groupTableSection.classList.add("hidden");
  }
}

function formatNumber(value) {
  if (typeof value !== "number") return value;
  return value.toFixed(4).replace(/\.?0+$/, "");
}

function clearResults() {
  emptyResult.classList.remove("hidden");
  resultContent.classList.add("hidden");
  decisionBadge.textContent = "Waiting";
  decisionBadge.className = "decision-badge neutral";
}

function showError(message) {
  errorBox.textContent = message;
  errorBox.classList.remove("hidden");
}

function clearError() {
  errorBox.classList.add("hidden");
  errorBox.textContent = "";
}

async function checkBackend() {
  try {
    const response = await fetch(`${API_BASE}/api/health`);
    const data = await response.json();

    if (response.ok && data.status === "ok") {
      backendStatus.textContent = "● VB.NET backend online";
      backendStatus.style.color = "var(--success)";
    }
  } catch {
    backendStatus.textContent = "● Backend offline";
    backendStatus.style.color = "var(--danger)";
  }
}

checkBackend();
