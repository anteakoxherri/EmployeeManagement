import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.jsx'

// Pika e nisjes se React: lidh komponentin App me elementin root te index.html.
// StrictMode ndihmon ne zbulimin e problemeve gjate zhvillimit; nuk vendos lejet e API-se.
createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
