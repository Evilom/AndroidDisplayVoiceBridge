package io.github.androiddisplayvoicebridge.adbkeeper;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

public final class BootReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(final Context context, Intent intent) {
        Log.i(AdbKeeper.TAG, "boot trigger received: " + intent.getAction());
        final PendingResult pending = goAsync();
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    AdbKeeper.repairIfNeeded(context.getApplicationContext(), false);
                    AdbKeeper.scheduleNextCheck(context.getApplicationContext(), 45L * 1000L);
                } finally {
                    pending.finish();
                }
            }
        }, "adb-keeper-boot").start();
    }
}
