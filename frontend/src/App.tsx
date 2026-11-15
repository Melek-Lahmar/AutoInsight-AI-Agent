import { useEffect, useState } from 'react'
import { Activity, AlertTriangle, BarChart3, Bot, CheckCircle2, ChevronRight, Database, Download, FileSearch, LayoutDashboard, LoaderCircle, LockKeyhole, Menu, MessageSquareText, Moon, Rows3, Send, ShieldCheck, Sparkles, Sun, Table2, WandSparkles, X } from 'lucide-react'
import { api } from './api'
import { ChartPanel } from './components/ChartPanel'
import { UploadZone } from './components/UploadZone'
import type { AgentResponse, AppConfig, ChartSpec, ColumnProfile, DatasetOverview, QualityFinding } from './types'

type View = 'dashboard' | 'dataset' | 'visuals' | 'agent' | 'reports'
const nav: { id: View; label: string; icon: typeof LayoutDashboard }[] = [
  { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'dataset', label: 'Dataset explorer', icon: Table2 },
  { id: 'visuals', label: 'Visual analytics', icon: BarChart3 },
  { id: 'agent', label: 'AI assistant', icon: MessageSquareText },
  { id: 'reports', label: 'Reports', icon: FileSearch },
]

function formatNumber(value: number) { return new Intl.NumberFormat('en').format(value) }

function App() {
  const [view, setView] = useState<View>('dashboard')
  const [overview, setOverview] = useState<DatasetOverview>()
  const [columns, setColumns] = useState<ColumnProfile[]>([])
  const [quality, setQuality] = useState<QualityFinding[]>([])
  const [config, setConfig] = useState<AppConfig>({ demoModeAvailable: true, aiModeConfigured: false, privacy: 'Local analysis' })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [dark, setDark] = useState(false)
  const [mobileNav, setMobileNav] = useState(false)

  useEffect(() => { api.getConfig().then(setConfig).catch(() => undefined) }, [])
  useEffect(() => { document.documentElement.dataset.theme = dark ? 'dark' : 'light' }, [dark])

  async function upload(file: File) {
    setBusy(true); setError('')
    try {
      const result = await api.upload(file)
      setOverview(result.overview); setColumns(result.columns)
      setQuality(await api.quality(result.overview.id)); setView('dataset')
    } catch (e) { setError(e instanceof Error ? e.message : 'Upload failed.') }
    finally { setBusy(false) }
  }

  const navigate = (next: View) => { setView(next); setMobileNav(false) }

  return (
    <div className="app-shell">
      <aside className={`sidebar ${mobileNav ? 'open' : ''}`}>
        <div className="brand"><div className="brand-mark"><Activity size={20} /></div><div><strong>AutoInsight</strong><span>AI Agent</span></div><button className="icon-button mobile-close" onClick={() => setMobileNav(false)}><X size={18} /></button></div>
        <div className="workspace-label">Workspace</div>
        <nav>{nav.map(item => <button key={item.id} className={view === item.id ? 'active' : ''} onClick={() => navigate(item.id)}><item.icon size={18} /><span>{item.label}</span>{view === item.id && <ChevronRight className="nav-arrow" size={15} />}</button>)}</nav>
        <div className="privacy-card"><div className="privacy-icon"><LockKeyhole size={17} /></div><div><strong>Privacy first</strong><p>Dataset processing stays local by default.</p></div></div>
        <div className="sidebar-footer"><span className="status-dot" /> API ready <span>v1.0</span></div>
      </aside>
      {mobileNav && <button className="scrim" aria-label="Close navigation" onClick={() => setMobileNav(false)} />}

      <main>
        <header className="topbar"><button className="icon-button mobile-menu" onClick={() => setMobileNav(true)}><Menu size={20} /></button><div className="breadcrumbs">AutoInsight <ChevronRight size={13} /> <strong>{nav.find(n => n.id === view)?.label}</strong></div><div className="top-actions"><span className={`mode-pill ${config.aiModeConfigured ? 'online' : ''}`}><span />{config.aiModeConfigured ? 'AI configured' : 'Demo mode'}</span><button className="icon-button" aria-label="Toggle theme" onClick={() => setDark(!dark)}>{dark ? <Sun size={18} /> : <Moon size={18} />}</button></div></header>
        <div className="content">
          {error && <div className="error-banner"><AlertTriangle size={18} /><span>{error}</span><button onClick={() => setError('')}><X size={16} /></button></div>}
          {view === 'dashboard' && <Dashboard overview={overview} columns={columns} quality={quality} onNavigate={navigate} onUpload={upload} busy={busy} />}
          {view === 'dataset' && <DatasetExplorer overview={overview} columns={columns} quality={quality} onUpload={upload} busy={busy} />}
          {view === 'visuals' && <VisualAnalytics overview={overview} columns={columns} onError={setError} />}
          {view === 'agent' && <AgentAssistant overview={overview} config={config} onError={setError} />}
          {view === 'reports' && <Reports overview={overview} quality={quality} onError={setError} />}
        </div>
      </main>
    </div>
  )
}

function PageIntro({ eyebrow, title, text, action }: { eyebrow: string; title: string; text: string; action?: React.ReactNode }) {
  return <div className="page-intro"><div><span className="eyebrow">{eyebrow}</span><h1>{title}</h1><p>{text}</p></div>{action}</div>
}

function EmptyDataset({ onNavigate }: { onNavigate?: (view: View) => void }) {
  return <div className="empty-state"><div><Database size={30} /></div><h2>No dataset loaded</h2><p>Upload a CSV to unlock profiling, visualizations, agent tools, and reports.</p>{onNavigate && <button className="button primary" onClick={() => onNavigate('dataset')}>Open dataset explorer</button>}</div>
}

function Dashboard({ overview, columns, quality, onNavigate, onUpload, busy }: { overview?: DatasetOverview; columns: ColumnProfile[]; quality: QualityFinding[]; onNavigate: (v: View) => void; onUpload: (f: File) => void; busy: boolean }) {
  const numeric = columns.filter(c => c.dataType === 'Numeric').length
  return <>
    <PageIntro eyebrow="Intelligent data workspace" title="From raw CSV to reliable insight." text="Profile data locally, surface quality risks, and let an allowlisted agent assemble evidence-backed analysis." action={<button className="button primary" onClick={() => onNavigate('agent')}><Sparkles size={16} /> Ask AutoInsight</button>} />
    {!overview ? <div className="dashboard-grid"><div className="span-2"><UploadZone onUpload={onUpload} busy={busy} /></div><div className="feature-stack"><div><ShieldCheck /><strong>Local-first analysis</strong><span>Raw rows stay in memory on your machine.</span></div><div><WandSparkles /><strong>Grounded agent tools</strong><span>Every statistic comes from executable C# functions.</span></div></div></div> : <>
      <div className="metric-grid"><Metric icon={Rows3} label="Rows" value={formatNumber(overview.rowCount)} note="observed records" /><Metric icon={Table2} label="Columns" value={String(overview.columnCount)} note={`${numeric} numeric`} /><Metric icon={AlertTriangle} label="Quality findings" value={String(quality.length)} note={`${overview.totalMissingValues} missing values`} /><Metric icon={CheckCircle2} label="Completeness" value={`${overview.rowCount * overview.columnCount === 0 ? 100 : (100 - overview.totalMissingValues * 100 / (overview.rowCount * overview.columnCount)).toFixed(1)}%`} note="calculated locally" /></div>
      <div className="dashboard-grid"><section className="panel span-2"><div className="section-heading"><div><span className="eyebrow">Current dataset</span><h2>{overview.fileName}</h2></div><button className="text-button" onClick={() => onNavigate('dataset')}>Explore data <ChevronRight size={15} /></button></div><Preview overview={overview} compact /></section><section className="panel"><div className="section-heading"><div><span className="eyebrow">Quality pulse</span><h2>What needs attention</h2></div></div>{quality.length ? <div className="finding-list">{quality.slice(0, 4).map((f, i) => <Finding key={`${f.code}-${i}`} finding={f} compact />)}</div> : <div className="all-clear"><CheckCircle2 /><strong>No rule-based warnings</strong><span>Review domain assumptions before drawing conclusions.</span></div>}</section></div>
    </>}
  </>
}

function Metric({ icon: Icon, label, value, note }: { icon: typeof Rows3; label: string; value: string; note: string }) {
  return <div className="metric"><div className="metric-icon"><Icon size={19} /></div><div><span>{label}</span><strong>{value}</strong><small>{note}</small></div></div>
}

function DatasetExplorer({ overview, columns, quality, onUpload, busy }: { overview?: DatasetOverview; columns: ColumnProfile[]; quality: QualityFinding[]; onUpload: (f: File) => void; busy: boolean }) {
  return <><PageIntro eyebrow="Dataset explorer" title="Understand the shape of your data." text="Upload a bounded CSV, inspect inferred types, and review deterministic quality checks before analysis." />
    {!overview ? <UploadZone onUpload={onUpload} busy={busy} /> : <div className="stack"><div className="dataset-toolbar"><div className="file-badge"><Database size={18} /><div><strong>{overview.fileName}</strong><span>{overview.rowCount} rows · {overview.columnCount} columns · “{overview.delimiter}” delimiter</span></div></div><label className="button secondary">Replace dataset<input aria-label="Replace CSV" type="file" accept=".csv" hidden onChange={e => e.target.files?.[0] && onUpload(e.target.files[0])} /></label></div><section className="panel"><div className="section-heading"><div><span className="eyebrow">Preview</span><h2>First {overview.preview.length} rows</h2></div><span className="safe-label"><ShieldCheck size={14} /> Read only</span></div><Preview overview={overview} /></section>
      <section className="panel"><div className="section-heading"><div><span className="eyebrow">Schema</span><h2>Column profiles</h2></div><span className="muted">{columns.length} detected</span></div><div className="column-grid">{columns.map(c => <div className="column-card" key={c.name}><div><strong>{c.name}</strong><span className={`type ${c.dataType.toLowerCase()}`}>{c.dataType}</span></div><div className="column-stats"><span><b>{c.uniqueCount}</b> unique</span><span><b>{c.missingCount}</b> missing</span>{c.statistics && <span><b>{c.statistics.mean.toFixed(2)}</b> mean</span>}</div></div>)}</div></section>
      <section className="panel"><div className="section-heading"><div><span className="eyebrow">Data quality</span><h2>Review before cleaning</h2></div><span className="safe-label">No changes applied</span></div>{quality.length ? <div className="finding-list roomy">{quality.map((f, i) => <Finding key={`${f.code}-${i}`} finding={f} />)}</div> : <div className="all-clear horizontal"><CheckCircle2 /><div><strong>No automated warnings found</strong><span>The dataset passed the implemented rule-based checks.</span></div></div>}</section></div>}
  </>
}

function Preview({ overview, compact = false }: { overview: DatasetOverview; compact?: boolean }) {
  const headers = overview.preview[0] ? Object.keys(overview.preview[0]) : []
  return <div className="table-wrap"><table><thead><tr>{headers.map(h => <th key={h}>{h}</th>)}</tr></thead><tbody>{overview.preview.slice(0, compact ? 5 : 12).map((row, i) => <tr key={i}>{headers.map(h => <td key={h}>{row[h] ?? <span className="null">missing</span>}</td>)}</tr>)}</tbody></table>{!overview.preview.length && <div className="table-empty">Headers detected; this file contains no data rows.</div>}</div>
}

function Finding({ finding, compact = false }: { finding: QualityFinding; compact?: boolean }) {
  return <div className={`finding ${finding.severity}`}><div className="finding-mark"><AlertTriangle size={16} /></div><div><strong>{finding.title}{finding.column && <span> · {finding.column}</span>}</strong><p>{finding.explanation}</p>{!compact && <small>{finding.recommendation}</small>}</div><b>{finding.count}</b></div>
}

function VisualAnalytics({ overview, columns, onError }: { overview?: DatasetOverview; columns: ColumnProfile[]; onError: (v: string) => void }) {
  const [type, setType] = useState('bar'); const [x, setX] = useState(''); const [y, setY] = useState(''); const [chart, setChart] = useState<ChartSpec>(); const [loading, setLoading] = useState(false)
  const selectedX = x || columns[0]?.name || ''
  async function generate() { if (!overview || !selectedX) return; setLoading(true); onError(''); try { setChart(await api.visualization(overview.id, { type, xColumn: selectedX, yColumn: y || undefined })) } catch (e) { onError(e instanceof Error ? e.message : 'Chart generation failed.') } finally { setLoading(false) } }
  return <><PageIntro eyebrow="Visual analytics" title="Turn columns into clear signals." text="Chart specifications are calculated by the backend and rendered safely in the browser." />{!overview ? <EmptyDataset /> : <div className="visual-layout"><section className="panel controls"><h2>Chart builder</h2><label>Chart type<select value={type} onChange={e => setType(e.target.value)}><option value="bar">Category bars</option><option value="histogram">Histogram</option><option value="line">Line chart</option><option value="scatter">Scatter plot</option><option value="missing">Missing values</option></select></label><label>X column<select value={selectedX} onChange={e => setX(e.target.value)}>{columns.map(c => <option key={c.name}>{c.name}</option>)}</select></label>{(type === 'line' || type === 'scatter') && <label>Y column<select value={y} onChange={e => setY(e.target.value)}><option value="">Select a numeric column</option>{columns.filter(c => c.dataType === 'Numeric').map(c => <option key={c.name}>{c.name}</option>)}</select></label>}<button className="button primary full" onClick={generate} disabled={loading}>{loading ? <LoaderCircle className="spin" size={16} /> : <BarChart3 size={16} />} Generate chart</button><div className="hint"><ShieldCheck size={16} /><span>Labels and values are escaped; no uploaded content is executed.</span></div></section><div>{chart ? <ChartPanel chart={chart} /> : <div className="chart-placeholder"><BarChart3 size={32} /><h2>Ready to visualize</h2><p>Choose a chart and columns, then generate a bounded specification.</p></div>}</div></div>}</>
}

function AgentAssistant({ overview, config, onError }: { overview?: DatasetOverview; config: AppConfig; onError: (v: string) => void }) {
  const [question, setQuestion] = useState('Analyze this dataset, identify the main data quality issues, calculate useful statistics, and generate a chart.')
  const [mode, setMode] = useState('demo'); const [response, setResponse] = useState<AgentResponse>(); const [loading, setLoading] = useState(false)
  async function send() { if (!overview || !question.trim()) return; setLoading(true); onError(''); try { setResponse(await api.chat(overview.id, question, mode)) } catch (e) { onError(e instanceof Error ? e.message : 'Agent request failed.') } finally { setLoading(false) } }
  return <><PageIntro eyebrow="Analysis assistant" title="Ask questions. Inspect every tool call." text="Demo Mode uses deterministic intent patterns; AI Agent Mode uses Semantic Kernel function calling when credentials are configured." />{!overview ? <EmptyDataset /> : <div className="agent-layout"><section className="agent-chat"><div className="agent-banner"><div className="bot-avatar"><Bot size={21} /></div><div><strong>AutoInsight agent</strong><span>{mode === 'ai' ? 'Real LLM function calling' : 'Deterministic non-LLM demonstration'}</span></div><select aria-label="Agent mode" value={mode} onChange={e => setMode(e.target.value)}><option value="demo">Demo mode</option><option value="ai" disabled={!config.aiModeConfigured}>AI agent mode{!config.aiModeConfigured ? ' · not configured' : ''}</option></select></div><div className="conversation"><div className="message assistant"><div className="tiny-avatar"><Bot size={14} /></div><div>Dataset <strong>{overview.fileName}</strong> is ready. Ask for an overview, quality review, statistics, or a distribution chart.</div></div>{response && <div className="message assistant result"><div className="tiny-avatar"><Sparkles size={14} /></div><div>{response.message}</div></div>}{loading && <div className="agent-thinking"><LoaderCircle className="spin" /> Selecting and executing local tools…</div>}</div><div className="composer"><textarea value={question} maxLength={2000} onChange={e => setQuestion(e.target.value)} onKeyDown={e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send() } }} /><button aria-label="Send question" onClick={send} disabled={loading}><Send size={18} /></button></div></section><aside className="tool-panel"><span className="eyebrow">Execution trace</span><h2>Agent activity</h2>{response?.toolExecutions.length ? <div className="tool-list">{response.toolExecutions.map((tool, i) => <div key={`${tool.tool}-${i}`}><span>{i + 1}</span><div><strong>{tool.tool}</strong><p>{tool.summary}</p></div><CheckCircle2 size={16} /></div>)}</div> : <div className="trace-empty"><Activity /><p>Tool calls will appear here after an analysis request.</p></div>}<div className="privacy-note"><LockKeyhole size={15} /><span>{mode === 'demo' ? 'No external provider is used in Demo Mode.' : 'Only schemas and calculated summaries are exposed to the model through tools.'}</span></div></aside>{response?.chart && <div className="agent-chart"><ChartPanel chart={response.chart} /></div>}</div>}</>
}

function Reports({ overview, quality, onError }: { overview?: DatasetOverview; quality: QualityFinding[]; onError: (v: string) => void }) {
  const [loading, setLoading] = useState(false)
  async function download() { if (!overview) return; setLoading(true); onError(''); try { const blob = await api.report(overview.id); const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = `autoinsight-${overview.fileName.replace(/\.csv$/i, '')}-report.md`; link.click(); URL.revokeObjectURL(url) } catch (e) { onError(e instanceof Error ? e.message : 'Report generation failed.') } finally { setLoading(false) } }
  return <><PageIntro eyebrow="Reports" title="Package the evidence, not the hype." text="Export a reproducible Markdown summary with calculated statistics, quality findings, recommendations, and limitations." />{!overview ? <EmptyDataset /> : <div className="report-layout"><section className="report-preview"><div className="report-paper"><span className="report-kicker">AUTOINSIGHT / DATA REPORT</span><h1>{overview.fileName}</h1><p className="report-meta">Local analysis · {new Date(overview.uploadedAt).toLocaleDateString()}</p><hr/><h2>Dataset overview</h2><div className="report-numbers"><span><b>{overview.rowCount}</b> rows</span><span><b>{overview.columnCount}</b> columns</span><span><b>{overview.totalMissingValues}</b> missing</span></div><h2>Data quality observations</h2>{quality.slice(0, 4).map((f, i) => <p key={i}><strong>{f.title}{f.column ? ` — ${f.column}` : ''}.</strong> {f.explanation}</p>)}{!quality.length && <p>No rule-based findings were detected.</p>}<h2>Limitations</h2><p>Type inference and outlier detection are heuristic. Findings require domain review, and the original dataset is never modified.</p></div></section><aside className="panel report-actions"><div className="download-icon"><Download /></div><h2>Markdown analysis report</h2><p>Portable, human-readable, and ready for a GitHub project or academic appendix.</p><ul><li><CheckCircle2 /> Dataset overview</li><li><CheckCircle2 /> Statistical profiles</li><li><CheckCircle2 /> Quality findings</li><li><CheckCircle2 /> Explicit limitations</li></ul><button className="button primary full" onClick={download} disabled={loading}>{loading ? <LoaderCircle className="spin" size={16} /> : <Download size={16} />} Download .md report</button></aside></div>}</>
}

export default App
