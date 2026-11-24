// Runs the Phonon speech model for the overlay (see cheddarchat-lite-overlay.js).
// Browsers only start workers from the page's own origin, so this file just pulls in the worker of the
// phonon-tts package from the CDN. Keep the version in step with phononLibrary in Overlay.cshtml.
import 'https://cdn.jsdelivr.net/npm/phonon-tts@0.7.1/worker.js'
