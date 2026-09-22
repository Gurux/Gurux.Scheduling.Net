//
// --------------------------------------------------------------------------
//  Gurux Ltd
//
//
//
// Filename:        $HeadURL$
//
// Version:         $Revision$,
//                  $Date$
//                  $Author$
//
// Copyright (c) Gurux Ltd
//
//---------------------------------------------------------------------------
//
//  DESCRIPTION
//
// This file is a part of Gurux Device Framework.
//
// Gurux Device Framework is Open Source software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; version 2 of the License.
// Gurux Device Framework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
//
// More information of Gurux products: https://www.gurux.org
//
// This code is licensed under the GNU General Public License v2.
// Full text may be retrieved at http://www.gnu.org/licenses/gpl-2.0.txt
//---------------------------------------------------------------------------

namespace Gurux.Scheduling
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Notifies when a scheduled date and time occurs.
    /// </summary>
    public sealed class GXScheduleNotifier : IDisposable
    {
        private readonly object _sync = new();
        private readonly List<GXDateTime> _schedules = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _task;

        /// <summary>
        /// Occurs when a scheduled date and time is reached.
        /// </summary>
        public event EventHandler<GXDateTime>? Triggered;

        /// <summary>
        /// Gets the scheduled date and time values.
        /// </summary>
        public IList<GXDateTime> Schedules => _schedules;

        /// <summary>
        /// Gets whether the notifier is running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_sync)
                {
                    return _task != null && !_task.IsCompleted;
                }
            }
        }

        /// <summary>
        /// Starts the schedule notifier.
        /// </summary>
        public void Start()
        {
            lock (_sync)
            {
                if (_task != null && !_task.IsCompleted)
                {
                    return;
                }
                _cancellationTokenSource = new CancellationTokenSource();
                _task = RunAsync(_cancellationTokenSource.Token);
            }
        }

        /// <summary>
        /// Stops the schedule notifier.
        /// </summary>
        public void Stop()
        {
            lock (_sync)
            {
                _cancellationTokenSource?.Cancel();
            }
        }

        /// <summary>
        /// Runs the schedule notifier.
        /// </summary>
        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    GXDateTime? next = GetNextSchedule();

                    if (next == null)
                    {
                        return;
                    }

                    TimeSpan delay = GetTimeUntilNextSchedule(next);

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Triggered?.Invoke(this, next);
                    }
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Normal cancellation.
            }
        }

        /// <summary>
        /// Gets the next scheduled date and time.
        /// </summary>
        /// <returns>
        /// The next scheduled date and time, or null if no schedule exists.
        /// </returns>
        private GXDateTime? GetNextSchedule()
        {
            GXDateTime? next = null;
            TimeSpan? shortest = null;

            foreach (GXDateTime schedule in _schedules)
            {
                TimeSpan delay = GetTimeUntilNextSchedule(schedule);

                if (delay < TimeSpan.Zero)
                {
                    continue;
                }

                if (shortest == null || delay < shortest.Value)
                {
                    shortest = delay;
                    next = schedule;
                }
            }

            return next;
        }

        /// <summary>
        /// Gets the time remaining until the next scheduled occurrence.
        /// </summary>
        /// <param name="value">
        /// The date and time used to determine the next scheduled occurrence.
        /// </param>
        /// <returns>
        /// The time remaining until the next scheduled occurrence.
        /// </returns>
        private static TimeSpan GetTimeUntilNextSchedule(GXDateTime value)
        {
            // Use the actual GXDateTime schedule calculation here.
            return value.Value - DateTime.Now;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_sync)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }
    }
}
