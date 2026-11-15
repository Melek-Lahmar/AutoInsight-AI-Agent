import { chromium } from '@playwright/test'
import { access, mkdir } from 'node:fs/promises'
import path from 'node:path'
import process from 'node:process'

const projectRoot = path.resolve(import.meta.dirname, '..', '..')
const outputDirectory = path.join(projectRoot, 'docs', 'screenshots')
const sampleDataset = path.join(projectRoot, 'sample-data', 'students_performance.csv')
const frontendUrl = process.env.FRONTEND_URL ?? 'http://localhost:5173'
const edgeCandidates = [
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
]

async function firstExisting(paths) {
  for (const candidate of paths) {
    try { await access(candidate); return candidate } catch { /* try the next supported installation path */ }
  }
  throw new Error('Microsoft Edge was not found. Set up a Playwright browser or update the screenshot script for this machine.')
}

await mkdir(outputDirectory, { recursive: true })
const browser = await chromium.launch({ executablePath: await firstExisting(edgeCandidates), headless: true })
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 1 })

try {
  await page.goto(frontendUrl, { waitUntil: 'networkidle' })
  await page.screenshot({ path: path.join(outputDirectory, 'csv-upload.png'), fullPage: true })

  await page.getByLabel('CSV file').setInputFiles(sampleDataset)
  await page.getByText('students_performance.csv', { exact: true }).waitFor()
  await page.screenshot({ path: path.join(outputDirectory, 'dataset-overview.png'), fullPage: true })

  const schemaPanel = page.locator('section.panel').filter({ hasText: 'Column profiles' })
  await schemaPanel.screenshot({ path: path.join(outputDirectory, 'statistical-analysis.png') })

  await page.getByRole('button', { name: 'Dashboard' }).click()
  await page.getByText('Quality pulse', { exact: true }).waitFor()
  await page.screenshot({ path: path.join(outputDirectory, 'dashboard.png'), fullPage: true })

  await page.getByRole('button', { name: 'Visual analytics' }).click()
  await page.getByText('Chart builder', { exact: true }).waitFor()
  await page.locator('label').filter({ hasText: 'Chart type' }).locator('select').selectOption('histogram')
  await page.locator('label').filter({ hasText: 'X column' }).locator('select').selectOption('FinalGrade')
  await page.getByRole('button', { name: 'Generate chart' }).click()
  await page.getByText('Histogram of FinalGrade', { exact: true }).waitFor()
  await page.screenshot({ path: path.join(outputDirectory, 'visualizations.png'), fullPage: true })

  await page.getByRole('button', { name: 'AI assistant' }).click()
  await page.getByRole('button', { name: 'Send question' }).click()
  await page.getByText('get_dataset_overview', { exact: true }).waitFor()
  await page.screenshot({ path: path.join(outputDirectory, 'ai-assistant-trace.png'), fullPage: true })

  await page.getByRole('button', { name: 'Reports' }).click()
  await page.getByText('Markdown analysis report', { exact: true }).waitFor()
  await page.screenshot({ path: path.join(outputDirectory, 'reports.png'), fullPage: true })
} finally {
  await browser.close()
}

console.log(`Captured genuine application screenshots in ${outputDirectory}`)
