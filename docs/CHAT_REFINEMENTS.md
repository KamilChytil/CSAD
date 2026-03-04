# Chat Experience Refinements

This document summarizes the recent improvements made to the chat system to enhance usability for both bankers and clients.

## 1. Persistent System Messages
Significant chat events are now automatically recorded and displayed as system messages within the chat thread. These messages are saved to the database, ensuring they persist across page refreshes.

- **Banker Joined**: "Bankéř [Jméno] se připojil k chatu."
- **Chat Transferred**: "Chat byl předán bankéři [Jméno]."
- **Chat Closed**: "Chat byl uzavřen."
- **Chat Reopened**: "Chat byl znovu otevřen."

## 2. Role-Based Message Coloring
To help distinguish between participants in a conversation (especially in support chats involving multiple bankers), messages now use a color-coding scheme:

- **Red (Right)**: Messages sent by you.
- **Gray (Left)**: Messages received from the client/supported user.
- **Light Blue (Left)**: Messages received from other bankers (in Support chats).

## 3. Improved Send Button Responsiveness
The "Odeslat" (Send) button now enables immediately as you start typing. Previously, it required clicking out of the text box to update its state. This provides a more fluid and responsive messaging experience.

## 4. Technical Fixes & Stability
- **Database Schema**: Added `IsSystem` and `ReadAt` columns to the `messages` table to support permanent system notifications and read receipts.
- **Client Connectivity**: Resolved a critical 500 error that prevented clients from initiating or viewing chats due to missing database columns.
- **Reliable Broadcasting**: Integrated SignalR broadcasting into all chat state transition handlers (Assign, Close, Reopen) for real-time UI updates.
