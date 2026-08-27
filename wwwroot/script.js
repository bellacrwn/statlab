/* StatLab 2.0 - Frontend Logic */
const state = {
  test: "sign",
  history: JSON.parse(localStorage.getItem("statlab_history") || "[]"),
  lastResult: null
};

const API_BASE = window.location.protocol === "file:" ? "http://localhost:5000" : "";

const config = {
  sign: {
    title: "Sign Test",
    pill: "Median • Exact Binomial",
    description: "Tests whether population median differs from hypothesized value using only direction of differences. Most robust, lowest power.",
    sample: "45, 51, 62, 48, 55, 59, 42, 67, 53, 49, 61, 57, 60, 44, 58",
    needsMedian: true,
    needsGroups: false,
    groupsMin: 0
  },
  "signed-rank": {
    title: "Wilcoxon Signed-Rank Test",
    pill: "Ranked differences • Tie corrected",
    description: "More powerful than Sign Test by ranking magnitudes. Requires symmetric distribution of differences.",
    sample: "52, 48, 61, 57, 55, 64, 46, 58, 62, 51, 59, 66, 54, 60, 63",
    needsMedian: true,
    needsGroups: false,
    groupsMin: 0
  },
  "kruskal-wallis": {
    title: "Kruskal-Wallis H Test",
    pill: "3+ independent • χ² approx",
    description: "Nonparametric ANOVA for 3+ independent groups. Tests if at least one group stochastically dominates.",
    sample: "",
    needsMedian: false,
    needsGroups: true,
    groupsMin: 3
  },
};

// DOM
const $ = s => document.querySelector(s);
const $$ = s => [...document.querySelectorAll(s)];
const tabs = $$(".test-tab");
const testTitle = $("#testTitle");
const testDescription = $("#testDescription");
const methodPill = $("#methodPill");
const values = $("#values");
const valueCount = $("#valueCount");
const median = $("#median");
const medianField = $("#medianField");
const alpha = $("#alpha");
const alphaCustom = $("#alphaCustom");
const alternative = $("#alternative");
const alternativeField = $("#alternativeField");
const singleInputArea = $("#singleInputArea");
const groupInputArea = $("#groupInputArea");
const groupsContainer = $("#groupsContainer");
const analyzeBtn = $("#analyzeBtn");
const sampleBtn = $("#sampleBtn");
const clearBtn = $("#clearBtn");
const addGroupBtn = $("#addGroup");
const clearGroupsBtn = $("#clearGroups");
const errorBox = $("#errorBox");
const warningBox = $("#warningBox");
const emptyResult = $("#emptyResult");
const resultContent = $("#resultContent");
const statGrid = $("#statGrid");
const decisionBadge = $("#decisionBadge");
const decisionCard = $("#decisionCard");
const effectBadge = $("#effectBadge");
const conclusion = $("#conclusion");
const interpretation = $("#interpretation");
const methodText = $("#methodText");
const assumptionsList = $("#assumptionsList");
const groupTableSection = $("#groupTableSection");
const groupTableBody = $("#groupTableBody");
const descriptiveSection = $("#descriptiveSection");
const descriptiveBody = $("#descriptiveBody");
const backendStatus = $("#backendStatus");
const dropZone = $("#dropZone");
const fileInput = $("#fileInput");
const exportGroup = $("#exportGroup");
const historySection = $("#historySection");
const historyList = $("#historyList");

// Theme
const themeToggle = $("#themeToggle");
const savedTheme = localStorage.getItem("statlab_theme") || "dark";
document.documentElement.setAttribute("data-theme", savedTheme);
themeToggle?.addEventListener("click", () => {
  const cur = document.documentElement.getAttribute("data-theme");
  const next = cur === "dark" ? "light" : "dark";
  document.documentElement.setAttribute("data-theme", next);
  localStorage.setItem("statlab_theme", next);
});

// Utils
function parseNumbers(text) {
  if (!text) return [];
  return text
    .split(/[\s,;\t\n\r]+/)
    .map(v => v.trim())
    .filter(Boolean)
    .map(v => {
      // handle European decimal comma? Already split by comma, so not
      const n = Number(v);
      return Number.isFinite(n) ? n : null;
    })
    .filter(v => v !== null);
}

function formatNumber(v, digits = 4) {
  if (typeof v !== "number" || !isFinite(v)) return v;
  if (Math.abs(v) < 0.0001 && v !== 0) return v.toExponential(3);
  const fixed = v.toFixed(digits);
  return fixed.replace(/\.?0+$/, "");
}

function updateValueCount() {
  const nums = parseNumbers(values.value);
  valueCount.textContent = `${nums.length} values`;
  if (nums.length > 0) {
    const min = Math.min(...nums);
    const max = Math.max(...nums);
    valueCount.textContent += ` • min ${formatNumber(min)} • max ${formatNumber(max)}`;
  }
}

function setTest(test) {
  state.test = test;
  tabs.forEach(tab => tab.classList.toggle("active", tab.dataset.test === test));
  const c = config[test];
  testTitle.textContent = c.title;
  methodPill.textContent = c.pill;
  testDescription.textContent = c.description;

  const needsGroups = c.needsGroups;
  singleInputArea.classList.toggle("hidden", needsGroups);
  groupInputArea.classList.toggle("hidden", !needsGroups);
  medianField.classList.toggle("hidden", !c.needsMedian);

  addGroupBtn.classList.remove("hidden");
  $("#groupHint").textContent = "Each group = independent sample. At least 3 are recommended for Kruskal-Wallis.";

  if (needsGroups && groupsContainer.children.length === 0) {
    initGroups(test);
  }

  clearResults();
  clearError();
}

function initGroups(testType) {
  groupsContainer.innerHTML = "";
  const cfg = config[testType];
  const count = cfg.groupsMin || 3;
  for (let i = 0; i < count; i++) {
    addGroupUI(i + 1);
  }
}

function addGroupUI(num) {
  const div = document.createElement("div");
  div.className = "group-item";
  const n = num || groupsContainer.children.length + 1;
  div.innerHTML = `
    <div class="group-item-header">
      <strong>Group ${n}</strong>
      <button type="button" class="remove-group" title="Remove group">✕</button>
    </div>
    <textarea class="group-box" placeholder="e.g. 12, 15, 13, 14, 16"></textarea>
    <div class="group-meta"><small class="group-count">0 values</small></div>
  `;
  groupsContainer.appendChild(div);

  const textarea = div.querySelector("textarea");
  const countEl = div.querySelector(".group-count");
  textarea.addEventListener("input", () => {
    const nums = parseNumbers(textarea.value);
    countEl.textContent = `${nums.length} values${nums.length ? ` • mean ${formatNumber(nums.reduce((a,b)=>a+b,0)/nums.length)}` : ""}`;
  });
  div.querySelector(".remove-group").addEventListener("click", () => {
    if (groupsContainer.children.length <= 2) {
      showWarning("At least 2 groups required.");
      return;
    }
    div.remove();
    renumberGroups();
  });
}

function renumberGroups() {
  [...groupsContainer.children].forEach((el, i) => {
    el.querySelector("strong").textContent = `Group ${i + 1}`;
  });
}

// Events
tabs.forEach(tab => tab.addEventListener("click", () => setTest(tab.dataset.test)));

sampleBtn.addEventListener("click", () => {
  if (config[state.test].needsGroups) {
    const samples = {
      "kruskal-wallis": [
        "12, 15, 13, 14, 16, 18, 11",
        "19, 17, 20, 18, 21, 22, 19",
        "10, 11, 9, 12, 8, 10, 9"
      ],
    };
    const s = samples[state.test] || samples["kruskal-wallis"];
    // ensure enough groups
    while (groupsContainer.children.length < s.length) addGroupUI();
    [...groupsContainer.querySelectorAll(".group-box")].forEach((box, i) => {
      if (s[i]) {
        box.value = s[i];
        box.dispatchEvent(new Event("input"));
      }
    });
  } else {
    values.value = config[state.test].sample;
    updateValueCount();
  }
});

clearBtn.addEventListener("click", () => {
  values.value = "";
  $$(".group-box").forEach(b => {
    b.value = "";
    b.dispatchEvent(new Event("input"));
  });
  updateValueCount();
  clearResults();
  clearError();
});

clearGroupsBtn?.addEventListener("click", () => {
  $$(".group-box").forEach(b => {
    b.value = "";
    b.dispatchEvent(new Event("input"));
  });
});

addGroupBtn.addEventListener("click", () => {
  addGroupUI();
});

values.addEventListener("input", updateValueCount);
$("#formatBtn")?.addEventListener("click", () => {
  const nums = parseNumbers(values.value);
  if (nums.length) values.value = nums.join(", ");
  updateValueCount();
});

// Alpha custom
alpha.addEventListener("change", () => {
  if (alpha.value === "custom") {
    alphaCustom.classList.remove("hidden");
    alphaCustom.focus();
  } else {
    alphaCustom.classList.add("hidden");
  }
});

// Drag & drop
dropZone.addEventListener("click", () => fileInput.click());
dropZone.addEventListener("dragover", e => {
  e.preventDefault();
  dropZone.classList.add("dragover");
});
dropZone.addEventListener("dragleave", () => dropZone.classList.remove("dragover"));
dropZone.addEventListener("drop", e => {
  e.preventDefault();
  dropZone.classList.remove("dragover");
  const file = e.dataTransfer.files[0];
  if (file) handleFile(file);
});
fileInput.addEventListener("change", () => {
  if (fileInput.files[0]) handleFile(fileInput.files[0]);
});

function handleFile(file) {
  const reader = new FileReader();
  reader.onload = ev => {
    const text = ev.target.result;
    if (config[state.test].needsGroups) {
      // Try to parse CSV rows as groups? Simple: if contains newlines and commas, treat each line as group?
      const lines = text.split("\n").map(l => l.trim()).filter(Boolean);
      if (lines.length >= 2) {
        groupsContainer.innerHTML = "";
        lines.slice(0, 10).forEach((line, i) => {
          addGroupUI(i + 1);
          const box = groupsContainer.lastElementChild.querySelector("textarea");
          box.value = line;
          box.dispatchEvent(new Event("input"));
        });
        showWarning(`Loaded ${lines.length} groups from file.`);
      } else {
        // single group data
        values.value = text;
        updateValueCount();
      }
    } else {
      values.value = text;
      updateValueCount();
    }
  };
  reader.readAsText(file);
}

// Analysis
analyzeBtn.addEventListener("click", async () => {
  clearError();

  let alphaVal = alpha.value === "custom" ? Number(alphaCustom.value) : Number(alpha.value);
  if (!(alphaVal > 0 && alphaVal < 1)) {
    showError("Alpha must be between 0 and 1.");
    return;
  }

  let payload = {
    test: state.test,
    alpha: alphaVal,
    median: Number(median.value),
    alternative: alternative.value
  };

  if (config[state.test].needsGroups) {
    const groups = [...document.querySelectorAll(".group-box")]
      .map(box => parseNumbers(box.value))
      .filter(g => g.length > 0);

    if (groups.length < 2) {
      showError("Please enter at least two groups with numeric data.");
      return;
    }
    payload.groups = groups;
  } else {
    const vals = parseNumbers(values.value);
    if (vals.length < 1) {
      showError("Please enter at least one numeric observation.");
      return;
    }
    if (vals.length < 5) {
      showWarning("Small sample (n<5) — p-values are approximate. Consider exact tables for validation.");
    }
    payload.values = vals;
  }

  analyzeBtn.disabled = true;
  analyzeBtn.innerHTML = `<span class="btn-icon">◷</span> Calculating...`;

  try {
    const res = await fetch(`${API_BASE}/api/analyze`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || "Backend failed to complete analysis.");

    renderResult(data, payload);
    addToHistory(data, payload);
  } catch (err) {
    showError(err.message);
  } finally {
    analyzeBtn.disabled = false;
    analyzeBtn.innerHTML = `<span class="btn-icon">▶</span> Run Analysis`;
  }
});

function renderResult(data, payload) {
  state.lastResult = data;
  emptyResult.classList.add("hidden");
  resultContent.classList.remove("hidden");
  exportGroup.classList.remove("hidden");

  // Stats grid
  const items = [];
  if (data.test === "Sign Test") {
    items.push(["n (excl ties)", data.n]);
    items.push(["Positive", data.positive]);
    items.push(["Negative", data.negative]);
    items.push(["Ties", data.ties]);
    items.push(["Statistic k", data.statistic]);
    items.push(["p-value", formatNumber(data.pValue)]);
    items.push(["Effect size", formatNumber(data.effectSize)]);
  } else if (data.test === "Wilcoxon Signed-Rank Test") {
    items.push(["n (non-zero)", data.n]);
    items.push(["W⁺", formatNumber(data.wPlus)]);
    items.push(["W⁻", formatNumber(data.wMinus)]);
    items.push(["W", formatNumber(data.statistic)]);
    items.push(["z", formatNumber(data.z)]);
    items.push(["p-value", formatNumber(data.pValue)]);
    items.push(["r (effect)", formatNumber(data.effectSize)]);
  } else if (data.test === "Kruskal-Wallis H Test") {
    items.push(["Groups", data.groups]);
    items.push(["Total N", data.n]);
    items.push(["H", formatNumber(data.statistic)]);
    items.push(["df", data.degreesOfFreedom]);
    items.push(["p-value", formatNumber(data.pValue)]);
    items.push(["η²", formatNumber(data.effectSize)]);
  }

  statGrid.innerHTML = items.map(([label, val]) => `
    <div class="stat-card"><small>${label}</small><strong>${val}</strong></div>
  `).join("");

  const rejected = data.decision.toLowerCase().includes("reject");
  decisionBadge.textContent = data.decision;
  decisionBadge.className = `decision-badge ${rejected ? "reject" : "accept"}`;
  decisionCard.textContent = `${data.decision} at α = ${data.alpha} (${data.alternative ? data.alternative : "two-sided"}).`;

  // effect badge
  if (data.effectSize !== undefined) {
    let interp = "";
    const es = data.effectSize;
    if (data.test.includes("Kruskal")) {
      interp = es < 0.01 ? "negligible" : es < 0.06 ? "small" : es < 0.14 ? "medium" : "large";
    } else {
      interp = es < 0.1 ? "negligible" : es < 0.3 ? "small" : es < 0.5 ? "medium" : "large";
    }
    effectBadge.textContent = `Effect ${formatNumber(es)} • ${interp}`;
  }

  conclusion.textContent = data.conclusion;
  interpretation.textContent = data.interpretation || "";
  methodText.textContent = data.method || "";
  assumptionsList.innerHTML = (data.assumptions || []).map(a => `<li>${a}</li>`).join("");

  // Descriptive
  if (data.descriptive) {
    descriptiveSection.classList.remove("hidden");
    const d = data.descriptive;
    descriptiveBody.innerHTML = `
      <tr><td>Count</td><td>${d.Count}</td></tr>
      <tr><td>Mean</td><td>${formatNumber(d.Mean)}</td></tr>
      <tr><td>Median</td><td>${formatNumber(d.Median)}</td></tr>
      <tr><td>Std Dev</td><td>${formatNumber(d.StdDev)}</td></tr>
      <tr><td>Min / Max</td><td>${formatNumber(d.Min)} / ${formatNumber(d.Max)}</td></tr>
      <tr><td>Q1 / Q3</td><td>${formatNumber(d.Q1)} / ${formatNumber(d.Q3)}</td></tr>
    `;
  } else {
    descriptiveSection.classList.add("hidden");
  }

  // Group table
  if (data.groupSummaries) {
    groupTableSection.classList.remove("hidden");
    groupTableBody.innerHTML = data.groupSummaries.map(g => `
      <tr>
        <td>${g.group}</td>
        <td>${g.n}</td>
        <td>${formatNumber(g.rankSum)}</td>
        <td>${formatNumber(g.meanRank || g.rankSum / g.n)}</td>
        <td>${formatNumber(g.mean)}</td>
        <td>${formatNumber(g.median)}</td>
      </tr>
    `).join("");
  } else {
    groupTableSection.classList.add("hidden");
  }

  // Charts
  setTimeout(() => drawCharts(data, payload), 50);

  // Hero mini update
  $("#heroN").textContent = data.n || data.groups || "—";
  $("#heroP").textContent = formatNumber(data.pValue);
  $("#heroD").textContent = rejected ? "Reject" : "Retain";
}

function drawCharts(data, payload) {
  const distCanvas = $("#distChart");
  const rankCanvas = $("#rankChart");
  if (!distCanvas || !rankCanvas) return;

  // Distribution chart - simple histogram / boxplot
  const ctx1 = distCanvas.getContext("2d");
  const w = distCanvas.width, h = distCanvas.height;
  ctx1.clearRect(0,0,w,h);

  // background
  ctx1.fillStyle = getComputedStyle(document.documentElement).getPropertyValue("--surface-2") || "#1c2330";
  ctx1.fillRect(0,0,w,h);

  let valuesToPlot = [];
  if (payload.values) valuesToPlot = payload.values;
  else if (payload.groups) valuesToPlot = payload.groups.flat();

  if (valuesToPlot.length) {
    const min = Math.min(...valuesToPlot);
    const max = Math.max(...valuesToPlot);
    const range = max - min || 1;
    const bins = 12;
    const hist = new Array(bins).fill(0);
    valuesToPlot.forEach(v => {
      const idx = Math.min(bins-1, Math.floor((v - min) / range * bins));
      hist[idx]++;
    });
    const maxCount = Math.max(...hist);
    const barW = w / bins;

    // grid
    ctx1.strokeStyle = "rgba(255,255,255,0.06)";
    ctx1.lineWidth = 1;
    for (let i=0;i<=4;i++) {
      const y = h * i / 4;
      ctx1.beginPath(); ctx1.moveTo(0,y); ctx1.lineTo(w,y); ctx1.stroke();
    }

    // bars
    hist.forEach((c,i) => {
      const barH = (c / maxCount) * (h - 30);
      const x = i * barW + 2;
      const y = h - barH - 20;
      ctx1.fillStyle = data.decision?.includes("Reject") ? "#ff6b6b" : "#d9ff4f";
      if (document.documentElement.getAttribute("data-theme")==="light") {
        ctx1.fillStyle = data.decision?.includes("Reject") ? "#dc2626" : "#0f172a";
      }
      ctx1.globalAlpha = 0.8;
      ctx1.fillRect(x, y, barW-4, barH);
      ctx1.globalAlpha = 1;
    });

    // median line
    if (payload.median !== undefined && !payload.groups) {
      const medX = ((payload.median - min) / range) * w;
      if (medX >=0 && medX <= w) {
        ctx1.strokeStyle = "#ffcc66";
        ctx1.setLineDash([4,4]);
        ctx1.beginPath(); ctx1.moveTo(medX,0); ctx1.lineTo(medX,h); ctx1.stroke();
        ctx1.setLineDash([]);
        ctx1.fillStyle = "#ffcc66";
        ctx1.font = "10px monospace";
        ctx1.fillText(`H₀=${payload.median}`, medX+4, 12);
      }
    }
  }

  // Rank chart
  const ctx2 = rankCanvas.getContext("2d");
  ctx2.clearRect(0,0,rankCanvas.width, rankCanvas.height);
  ctx2.fillStyle = getComputedStyle(document.documentElement).getPropertyValue("--surface-2") || "#1c2330";
  ctx2.fillRect(0,0,rankCanvas.width, rankCanvas.height);

  if (data.groupSummaries) {
    const groups = data.groupSummaries;
    const maxRank = Math.max(...groups.map(g => g.meanRank || g.rankSum));
    const barH = (rankCanvas.height - 40) / groups.length;
    groups.forEach((g,i) => {
      const val = g.meanRank || g.rankSum;
      const barW = (val / maxRank) * (rankCanvas.width - 80);
      const y = i * barH + 20;
      ctx2.fillStyle = `hsl(${120 + i*30}, 70%, 60%)`;
      if (document.documentElement.getAttribute("data-theme")==="light") ctx2.fillStyle = `hsl(${220 + i*15}, 70%, 40%)`;
      ctx2.fillRect(60, y, barW, barH-8);
      ctx2.fillStyle = getComputedStyle(document.documentElement).getPropertyValue("--text") || "#fff";
      ctx2.font = "11px monospace";
      ctx2.fillText(`G${g.group}`, 4, y+14);
      ctx2.fillText(formatNumber(val), 60+barW+6, y+14);
    });
  } else {
    // for single sample, show positive vs negative
    if (data.positive !== undefined) {
      const total = data.positive + data.negative;
      const posW = (data.positive/total)* (rankCanvas.width-20);
      const negW = (data.negative/total)* (rankCanvas.width-20);
      ctx2.fillStyle = "#b7ef65";
      ctx2.fillRect(10, 30, posW, 30);
      ctx2.fillStyle = "#ff6b6b";
      ctx2.fillRect(10, 70, negW, 30);
      ctx2.fillStyle = "#fff";
      ctx2.font = "12px sans-serif";
      if (document.documentElement.getAttribute("data-theme")==="light") ctx2.fillStyle = "#000";
      ctx2.fillText(`Positive: ${data.positive}`, 12, 48);
      ctx2.fillText(`Negative: ${data.negative}`, 12, 88);
    } else if (data.wPlus !== undefined) {
      const maxW = Math.max(data.wPlus, data.wMinus);
      const wPlusW = (data.wPlus/maxW)*(rankCanvas.width-20);
      const wMinusW = (data.wMinus/maxW)*(rankCanvas.width-20);
      ctx2.fillStyle = "#b7ef65";
      ctx2.fillRect(10,30,wPlusW,30);
      ctx2.fillStyle = "#ff6b6b";
      ctx2.fillRect(10,70,wMinusW,30);
      ctx2.fillStyle = "#fff";
      if (document.documentElement.getAttribute("data-theme")==="light") ctx2.fillStyle = "#000";
      ctx2.fillText(`W+ ${formatNumber(data.wPlus)}`,12,48);
      ctx2.fillText(`W- ${formatNumber(data.wMinus)}`,12,88);
    }
  }

  // Hero chart also
  drawHeroChart(valuesToPlot);
}

function drawHeroChart(vals) {
  const canvas = $("#heroChart");
  if (!canvas || !vals.length) return;
  const ctx = canvas.getContext("2d");
  const w = canvas.width, h = canvas.height;
  ctx.clearRect(0,0,w,h);
  // simple sparkline
  const min = Math.min(...vals);
  const max = Math.max(...vals);
  const range = max-min || 1;
  ctx.strokeStyle = "#d9ff4f";
  if (document.documentElement.getAttribute("data-theme")==="light") ctx.strokeStyle = "#0f172a";
  ctx.lineWidth = 2;
  ctx.beginPath();
  vals.forEach((v,i) => {
    const x = (i/(vals.length-1))*w;
    const y = h - ((v-min)/range)*h;
    if (i===0) ctx.moveTo(x,y); else ctx.lineTo(x,y);
  });
  ctx.stroke();
  // fill
  ctx.lineTo(w,h); ctx.lineTo(0,h); ctx.closePath();
  ctx.fillStyle = "rgba(217,255,79,0.15)";
  if (document.documentElement.getAttribute("data-theme")==="light") ctx.fillStyle = "rgba(15,23,42,0.08)";
  ctx.fill();
}

function clearResults() {
  emptyResult.classList.remove("hidden");
  resultContent.classList.add("hidden");
  exportGroup.classList.add("hidden");
  decisionBadge.textContent = "Waiting";
  decisionBadge.className = "decision-badge neutral";
  $("#heroN").textContent = "—";
  $("#heroP").textContent = "—";
  $("#heroD").textContent = "—";
}

function showError(msg) {
  errorBox.textContent = msg;
  errorBox.classList.remove("hidden");
}
function showWarning(msg) {
  warningBox.textContent = msg;
  warningBox.classList.remove("hidden");
  setTimeout(() => warningBox.classList.add("hidden"), 4000);
}
function clearError() {
  errorBox.classList.add("hidden");
  errorBox.textContent = "";
  warningBox.classList.add("hidden");
}

// History
function addToHistory(result, payload) {
  const entry = {
    id: Date.now(),
    test: result.test,
    pValue: result.pValue,
    decision: result.decision,
    timestamp: new Date().toLocaleTimeString(),
    payload: payload
  };
  state.history.unshift(entry);
  state.history = state.history.slice(0, 20);
  localStorage.setItem("statlab_history", JSON.stringify(state.history));
  renderHistory();
}

function renderHistory() {
  if (!state.history.length) {
    historySection.classList.add("hidden");
    return;
  }
  historySection.classList.remove("hidden");
  historyList.innerHTML = state.history.map(h => `
    <div class="history-item" data-id="${h.id}">
      <div><strong>${h.test}</strong> • p=${formatNumber(h.pValue)} • ${h.decision}</div>
      <small>${h.timestamp}</small>
    </div>
  `).join("");

  $$(".history-item").forEach(el => {
    el.addEventListener("click", () => {
      const id = Number(el.dataset.id);
      const entry = state.history.find(x => x.id === id);
      if (!entry) return;
      // reload payload
      if (entry.payload.values) {
        setTest("sign"); // will be overwritten
        // try to infer test
        const map = {"Sign Test":"sign","Wilcoxon Signed-Rank Test":"signed-rank","Kruskal-Wallis H Test":"kruskal-wallis"};
        const testId = map[entry.test] || "sign";
        setTest(testId);
        if (entry.payload.values) {
          values.value = entry.payload.values.join(", ");
          updateValueCount();
        }
      }
      if (entry.payload.groups) {
        const map = {"Sign Test":"sign","Wilcoxon Signed-Rank Test":"signed-rank","Kruskal-Wallis H Test":"kruskal-wallis"};
        const testId = map[entry.test] || "kruskal-wallis";
        setTest(testId);
        groupsContainer.innerHTML = "";
        entry.payload.groups.forEach((g,i) => {
          addGroupUI(i+1);
          const box = groupsContainer.lastElementChild.querySelector("textarea");
          box.value = g.join(", ");
          box.dispatchEvent(new Event("input"));
        });
      }
      window.scrollTo({top: document.querySelector(".workspace").offsetTop - 20, behavior: "smooth"});
    });
  });
}

$("#clearHistory")?.addEventListener("click", () => {
  state.history = [];
  localStorage.removeItem("statlab_history");
  renderHistory();
});

// Export
$("#copyJsonBtn")?.addEventListener("click", async () => {
  if (!state.lastResult) return;
  await navigator.clipboard.writeText(JSON.stringify(state.lastResult, null, 2));
  const btn = $("#copyJsonBtn");
  const orig = btn.textContent;
  btn.textContent = "✓";
  setTimeout(() => btn.textContent = orig, 1000);
});

$("#exportCsvBtn")?.addEventListener("click", () => {
  if (!state.lastResult) return;
  const r = state.lastResult;
  let csv = `Test,${r.test}\nP-value,${r.pValue}\nStatistic,${r.statistic}\nDecision,${r.decision}\nAlpha,${r.alpha}\n`;
  if (r.groupSummaries) {
    csv += "\nGroup,n,RankSum,Mean,Median\n";
    r.groupSummaries.forEach(g => {
      csv += `${g.group},${g.n},${g.rankSum},${g.mean},${g.median}\n`;
    });
  }
  const blob = new Blob([csv], {type: "text/csv"});
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url; a.download = `statlab_${Date.now()}.csv`; a.click();
  URL.revokeObjectURL(url);
});

$("#shareBtn")?.addEventListener("click", async () => {
  if (!state.lastResult) {
    showWarning("Run an analysis first to share.");
    return;
  }
  const payload = {
    test: state.test,
    p: formatNumber(state.lastResult.pValue),
    decision: state.lastResult.decision
  };
  const text = `StatLab ${payload.test}: p=${payload.p} • ${payload.decision}`;
  if (navigator.share) {
    try { await navigator.share({title: "StatLab Result", text}); } catch {}
  } else {
    await navigator.clipboard.writeText(text);
    showWarning("Result copied to clipboard!");
  }
});

// Backend check
async function checkBackend() {
  const dot = backendStatus.querySelector(".status-dot");
  const txt = backendStatus.querySelector(".status-text");
  try {
    const res = await fetch(`${API_BASE}/api/health`);
    const data = await res.json();
    if (res.ok && data.status === "ok") {
      dot.className = "status-dot online";
      txt.textContent = `● VB.NET online • ${data.tests?.length || 3} tests`;
      backendStatus.style.color = "var(--success)";
    } else throw new Error();
  } catch {
    dot.className = "status-dot offline";
    txt.textContent = "● Backend offline — run dotnet";
    backendStatus.style.color = "var(--danger)";
  }
}

// Init
(function init() {
  setTest("sign");
  initGroups("sign");
  updateValueCount();
  renderHistory();
  checkBackend();
  setInterval(checkBackend, 15000);
  $("#footerYear").textContent = new Date().getFullYear();
})();
