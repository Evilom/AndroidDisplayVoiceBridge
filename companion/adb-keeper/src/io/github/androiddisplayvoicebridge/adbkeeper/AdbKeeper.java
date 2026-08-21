package io.github.androiddisplayvoicebridge.adbkeeper;

import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.os.SystemClock;
import android.provider.Settings;
import android.util.Log;

import java.io.IOException;
import java.net.InetSocketAddress;
import java.net.Socket;

final class AdbKeeper {
    static final String TAG = "AdbKeeper";
    static final String ACTION_CHECK =
            "io.github.androiddisplayvoicebridge.adbkeeper.CHECK";
    private static final int ADB_PORT = 5555;
    private static final long CHECK_INTERVAL_MS = 15L * 60L * 1000L;

    private AdbKeeper() {
    }

    static Result repairIfNeeded(Context context, boolean force) {
        boolean wasListening = isLocalAdbListening();
        boolean repaired = false;

        try {
            Settings.Global.putInt(
                    context.getContentResolver(),
                    Settings.Global.STAY_ON_WHILE_PLUGGED_IN,
                    7);

            int enabled = Settings.Global.getInt(
                    context.getContentResolver(), Settings.Global.ADB_ENABLED, 0);

            if (force || !wasListening) {
                if (enabled == 1) {
                    Settings.Global.putInt(
                            context.getContentResolver(), Settings.Global.ADB_ENABLED, 0);
                    SystemClock.sleep(500);
                }

                Settings.Global.putInt(
                        context.getContentResolver(), Settings.Global.ADB_ENABLED, 1);
                repaired = true;
                SystemClock.sleep(2500);
            }

            boolean listeningNow = isLocalAdbListening();
            Log.i(TAG, "check complete: before=" + wasListening
                    + ", repaired=" + repaired + ", after=" + listeningNow);
            return new Result(wasListening, repaired, listeningNow, null);
        } catch (SecurityException exception) {
            Log.e(TAG, "WRITE_SECURE_SETTINGS has not been granted", exception);
            return new Result(wasListening, repaired, false,
                    "缺少 WRITE_SECURE_SETTINGS 授权");
        } catch (RuntimeException exception) {
            Log.e(TAG, "repair failed", exception);
            return new Result(wasListening, repaired, false, exception.toString());
        }
    }

    static void scheduleNextCheck(Context context, long initialDelayMs) {
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarms == null) {
            Log.w(TAG, "AlarmManager unavailable");
            return;
        }

        Intent intent = new Intent(context, CheckReceiver.class).setAction(ACTION_CHECK);
        PendingIntent pendingIntent = PendingIntent.getBroadcast(
                context,
                5555,
                intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);

        alarms.setAndAllowWhileIdle(
                AlarmManager.ELAPSED_REALTIME_WAKEUP,
                SystemClock.elapsedRealtime() + initialDelayMs,
                pendingIntent);
    }

    static void scheduleRegularCheck(Context context) {
        scheduleNextCheck(context, CHECK_INTERVAL_MS);
    }

    private static boolean isLocalAdbListening() {
        Socket socket = new Socket();
        try {
            socket.connect(new InetSocketAddress("127.0.0.1", ADB_PORT), 700);
            return true;
        } catch (IOException ignored) {
            return false;
        } finally {
            try {
                socket.close();
            } catch (IOException ignored) {
                // Nothing to do.
            }
        }
    }

    static final class Result {
        final boolean listeningBefore;
        final boolean repaired;
        final boolean listeningAfter;
        final String error;

        Result(boolean listeningBefore, boolean repaired, boolean listeningAfter, String error) {
            this.listeningBefore = listeningBefore;
            this.repaired = repaired;
            this.listeningAfter = listeningAfter;
            this.error = error;
        }

        String describe() {
            if (error != null) {
                return "失败：" + error;
            }
            if (listeningAfter) {
                return repaired ? "ADB 5555 已重新启动" : "ADB 5555 正常，无需修复";
            }
            return "已写入 ADB 开关，但 5555 仍未监听";
        }
    }
}
