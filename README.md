# CheddarChat Lite

A free, minimal [BTCPay Server](https://github.com/btcpayserver/btcpayserver) plugin for streamers.

Viewers pay to send a message (a CheddarChat). Once the payment is settled, the message pops up as a donation alert in OBS and is read aloud by a natural sounding voice that runs inside the OBS browser source.

## Features

- **Public donation page:** Viewers enter their name, an amount and a message, then pay with any payment method enabled on your store.
- **Alerts on settled payments:** An alert fires at the same moment BTCPay Server fires its "Invoice settled" webhook. That is instantly for Lightning, and for on-chain payments it follows your store's confirmation setting.
- **OBS overlay:** A transparent browser source that shows your image or GIF (a bouncing Bitcoin logo by default), "*Name* sent *amount*" and the message, and reads the message aloud. Alerts that arrive while one is playing are queued.
- **Automatic or manual alerts:** Let alerts play automatically, or review each paid CheddarChat on the Alerts page and play it yourself. You can switch at any time, even mid-stream.
- **Alerts page:** Shows paid CheddarChats as they arrive, with Play/Replay, Skip current alert, Send test alert and the overlay connection status.
- **Natural voices without a speech service:** 37 English voices (American, British and characters like a pirate captain) of the open Phonon speech model, which runs on the streaming computer.
- One external JavaScript library, the Phonon runtime, and only in the OBS overlay. See [Text-to-speech](#text-to-speech).

## Setup

1. Install the plugin (see [Building](#building)) and open your store.
2. In the left navigation, click **CheddarChat Lite**, then **Enable**. Each store has one CheddarChat.
3. In the settings, set the currency, minimum amount, maximum message length, the **alert image URL** and whether text-to-speech is on. Save.
4. Click **Alerts** and copy the **OBS overlay URL**.
5. In OBS, add a **Browser** source with that URL, sized for example 800×600 or your full canvas size.
6. Click **Send test alert** to check the overlay.
7. Click **View** in the settings to get the public page and share it with your viewers.

The overlay URL contains a secret key. Keep it off-screen. If it leaks, use **Reset** on the Alerts page and paste the new URL into OBS.

<details>
<summary><strong>Use a clean link like donate.example.com</strong></summary>

The public page is at `https://your-btcpay-server/apps/<app id>/cheddarchat`. BTCPay Server can show it on a subdomain of a domain you own instead, for example `donate.example.com`. The admin of the server sets this up: see [How to map a domain name to an app](https://docs.btcpayserver.org/FAQ/Apps/#how-to-map-a-domain-name-to-an-app) in the BTCPay Server documentation.

The last step there asks for the app: enter the app id, which the settings show under **Use a clean link like donate.example.com**. Afterwards the old link forwards to the subdomain, and the OBS overlay URL stays as it is.

</details>

### Turning it off

- **Disable** in the settings turns off the public page and the overlay. The settings and both URLs are kept, and **Enable** brings them back.
- **Delete** removes the settings and both URLs for good. Enabling CheddarChat Lite again starts fresh, with new URLs.

### Automatic vs. manual

- **Automatic** (default): Paid CheddarChats play as soon as they settle. Use **Skip current alert** to cut one short.
- **Manual:** Paid CheddarChats are listed as **New** on the Alerts page and only play when you click **Play**.

A CheddarChat counts as played once it has been sent to a connected overlay. If no overlay is connected when it settles, it stays **New**, so you can play it later.

### Text-to-speech

Pick the **Voice** in the settings.

A link in a message is read as "link to x.com" instead of the whole address, whether it is written as `https://x.com/…`, `www.x.com` or just `x.com`. The alert still shows the message as it was written.

**Named voices (default: Easton)** are spoken by [Phonon 31m](https://huggingface.co/gradium/phonon-31m-en), an open speech model that runs inside the OBS browser source. No speech service is involved, and messages never leave the streaming computer.

- Its sound is part of the browser source: it shows up in the OBS audio mixer, with **Control audio via OBS** checked or not.
- The first time the overlay is opened, OBS downloads the model (about 70 MB in total). The overlay shows a progress bar in its bottom left corner meanwhile, so open it once before going live. On a fast connection this takes a few seconds. Afterwards OBS keeps the files, and the voice is ready a few seconds after OBS starts.
- Until the model is ready, and if it cannot be downloaded, alerts are read by the voice of the streaming computer.
- Amounts, numbers and abbreviations are read out properly.
- While the overlay is open, OBS uses about 400 MB more memory.

**Voice of the streaming computer** uses the browser's built-in speech synthesis. It needs no download, but its sound is played by the operating system instead of the page: OBS only gets it as desktop audio, and only with **Control audio via OBS** unchecked.

If you open the overlay URL in a regular browser tab to test it, click into the page once first: Browsers only allow sound after user interaction. OBS browser sources don't need this.

#### What the overlay loads from other servers

Only the overlay page does this, and only when a named voice is selected. The public donation page and the admin pages load nothing from outside your BTCPay Server.

| What | From | Size |
|---|---|---|
| Phonon runtime `phonon-tts` 0.7.1 (MIT or Apache-2.0) | cdn.jsdelivr.net | about 4 MB |
| Phonon 31m English model and the selected voice (Apache-2.0) | huggingface.co | about 65 MB |

Versions are pinned, so these addresses always return the same files. The requests come from the streamer's OBS, never from viewers.

### Customizing the look

The overlay uses the store's brand color (Store Settings → Branding) for the name and amount. Everything else can be changed in the browser source's **Custom CSS** field in OBS, for example:

```css
:root {
  --cc-accent: #ff4f9a;
  --cc-text: #fff;
  --cc-font: "Comic Sans MS", cursive;
}
.cc-alert { justify-content: flex-end; }   /* alerts at the bottom */
.cc-alert__image { max-height: 30vh; }
.cc-alert__image--default { animation: none; }   /* Bitcoin logo without the bounce */
```

Available classes: `.cc-alert`, `.cc-alert__image`, `.cc-alert__image--default` (the Bitcoin logo that is shown when no alert image is set), `.cc-alert__title`, `.cc-alert__name`, `.cc-alert__amount`, `.cc-alert__message`.

## How it works

- Each CheddarChat is a regular BTCPay invoice tagged with the app. The name and message are stored in the invoice metadata under `cheddarChat`, and the time it was played under `cheddarChatPlayedAt` (Unix time).
- A hosted service listens for settled invoices (`Confirmed` / `MarkedCompleted`) and, in automatic mode, sends the alert to the overlay.
- The overlay and the Alerts page get updates over WebSockets. The server pings every 25 seconds, and the overlay reconnects automatically if the connection drops.

## Building

This repository uses BTCPay Server as a Git submodule, pinned to v2.4.7, the minimum version the plugin supports.

```sh
git submodule update --init --recursive
dotnet build BTCPayServer.Plugins.CheddarChatLite
```

To create an installable package (`.btcpay`) that you can upload under Server Settings → Plugins:

```sh
dotnet publish BTCPayServer.Plugins.CheddarChatLite -c Release -o publish
dotnet run --project btcpayserver/BTCPayServer.PluginPacker -- publish BTCPayServer.Plugins.CheddarChatLite dist
```

### Running it in a BTCPay development environment

Start the dependencies from `btcpayserver/BTCPayServer.Tests` (`docker compose up dev`), then point BTCPay Server at the debug build in `btcpayserver/BTCPayServer/appsettings.dev.json`:

```json
{
  "DEBUG_PLUGINS": "/absolute/path/to/BTCPayServer.Plugins.CheddarChatLite/bin/Debug/net10.0/BTCPayServer.Plugins.CheddarChatLite.dll"
}
```

## Credits

Based on the [Shoutout plugin](https://github.com/dennisreimann/btcpayserver-plugin-shoutout) by Dennis Reimann (MIT), see [NOTICE.md](NOTICE.md).
