package io.github.androiddisplayvoicebridge.adbkeeper;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

public final class ShellReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        long delayMs = intent.getLongExtra("delay_ms", 30_000L);
        delayMs = Math.max(5_000L, Math.min(delayMs, 10L * 60L * 1000L));
        AdbKeeper.scheduleNextCheck(context.getApplicationContext(), delayMs);
        Log.i(AdbKeeper.TAG, "shell requested a check in " + delayMs + " ms");
    }
}
