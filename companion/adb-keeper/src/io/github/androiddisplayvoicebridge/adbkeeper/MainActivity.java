package io.github.androiddisplayvoicebridge.adbkeeper;

import android.app.Activity;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

public final class MainActivity extends Activity {
    private TextView status;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        LinearLayout layout = new LinearLayout(this);
        layout.setOrientation(LinearLayout.VERTICAL);
        layout.setGravity(Gravity.CENTER);
        int padding = dp(32);
        layout.setPadding(padding, padding, padding, padding);

        TextView title = new TextView(this);
        title.setText("ADB Keeper");
        title.setTextSize(28);
        layout.addView(title);

        status = new TextView(this);
        status.setText("正在检查……");
        status.setTextSize(18);
        status.setPadding(0, dp(24), 0, dp(24));
        layout.addView(status);

        Button repair = new Button(this);
        repair.setText("立即检查并修复");
        repair.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                runCheck(true);
            }
        });
        layout.addView(repair);

        setContentView(layout);
        runCheck(false);
    }

    private void runCheck(final boolean force) {
        status.setText("正在检查……");
        new Thread(new Runnable() {
            @Override
            public void run() {
                final AdbKeeper.Result result =
                        AdbKeeper.repairIfNeeded(getApplicationContext(), force);
                AdbKeeper.scheduleRegularCheck(getApplicationContext());
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        status.setText(result.describe());
                    }
                });
            }
        }, "adb-keeper-ui").start();
    }

    private int dp(int value) {
        return (int) (value * getResources().getDisplayMetrics().density + 0.5f);
    }
}
