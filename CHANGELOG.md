# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0]

### Added

- Public page to send a paid message (CheddarChat) with name, amount and message.
- One CheddarChat per store: enabled with one click from the store navigation, with Disable and Delete in its settings.
- The settings point to how the public page gets a clean link on a subdomain of your own, like donate.example.com.
- OBS browser source overlay with alert image (a bouncing Bitcoin logo by default), text-to-speech and alert queue.
- Automatic and manual alert modes, Alerts page with Play/Replay, Skip and test alerts.
- The OBS overlay URL is hidden like a password on the Alerts page, with an eye button to show it, so that it does not end up on stream by accident.
- Voice setting: 37 natural English voices of the Phonon 31m speech model, which runs inside the OBS browser source (runtime from jsDelivr, model from Hugging Face), or the voice of the streaming computer.
- The overlay shows a progress bar while the voice model is downloaded for the first time.
- Links in a message are read as "link to x.com" instead of the whole address.
