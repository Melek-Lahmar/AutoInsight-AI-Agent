import { useRef, useState } from 'react'
import { FileUp } from 'lucide-react'

export function UploadZone({ onUpload, busy }: { onUpload: (file: File) => void; busy: boolean }) {
  const input = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const choose = (files: FileList | null) => { if (files?.[0]) onUpload(files[0]) }
  return (
    <div className={`upload-zone ${dragging ? 'dragging' : ''}`} onDragOver={e => { e.preventDefault(); setDragging(true) }} onDragLeave={() => setDragging(false)} onDrop={e => { e.preventDefault(); setDragging(false); choose(e.dataTransfer.files) }}>
      <div className="upload-icon"><FileUp size={23} /></div>
      <h3>{busy ? 'Profiling your dataset…' : 'Drop a CSV dataset here'}</h3>
      <p>Comma, semicolon, tab, and pipe separators · up to 5 MB</p>
      <button className="button secondary" disabled={busy} onClick={() => input.current?.click()}>{busy ? 'Analyzing…' : 'Choose CSV file'}</button>
      <input ref={input} aria-label="CSV file" type="file" accept=".csv,text/csv" hidden onChange={e => choose(e.target.files)} />
    </div>
  )
}
