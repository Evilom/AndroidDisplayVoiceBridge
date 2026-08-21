package io.github.androiddisplayvoicebridge.adbkeeper;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

public final class CheckReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(final Context context, Intent intent) {
        final PendingResult pending = goAsync();
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    AdbKeeper.repairIfNeeded(context.getApplicationContext(), false);
                    AdbKeeper.scheduleRegularCheck(context.getApplicationContext());
                } finally {
                    pending.finish();
                }
            }
        }, "adb-keeper-check").start();
    }
}
