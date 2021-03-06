﻿using Akka.Actor;
using Akka.Event;
using Akka.Util.Internal;
using AkkaSim.Interfaces;
using AkkaSim.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using static AkkaSim.Definitions.SimulationMessage;
using System.Threading.Tasks;
using Akka;

namespace AkkaSim
{
    public class SimulationContext : ReceiveActor, IWithUnboundedStash
    {
        private Dictionary<long, long> _FeaturedInstructions = new Dictionary<long, long>();

        private long _CurrentInstructions = 0;

        //public HashSet<IActorRef> _allSimulants = new HashSet<IActorRef>();

        private bool _IsRunning = true;

        private bool _IsComplete = false;

        public IStash Stash { get; set; }

        private long TimePeriod { get; set; }

        /// <summary>
        /// Constructor for Simulation context
        /// </summary>
        /// <returns>IActorRef of the SimulationContext</returns>
        public static Props Props(EventStream eventStream)
        {
            return Akka.Actor.Props.Create(() => new SimulationContext());
        }

        public SimulationContext()
        {
            Receive<Command>(s => s == Command.Start, s =>
            {
                //Console.WriteLine("-- Starting Simulation -- !");
                Stash.UnstashAll();

                BecomeStacked(() =>
                {
                    Receive<Command>(c => c == Command.Done, c =>
                    {
                        //System.Diagnostics.Debug.WriteLine("-- Done");
                        _CurrentInstructions--;
                        Advance();
                    });

                    Receive<Command>(c => c == Command.Stop, c =>
                    {
                        // Console.WriteLine("-- Resume simulation -- !");
                        //System.Diagnostics.Debug.WriteLine("STOP");
                        Context.UnbecomeStacked();
                    });

                    Receive<Shutdown>(c => 
                    {
                        if (_CurrentInstructions == 0 && _FeaturedInstructions.Count() == 0)
                        {
                            //System.Diagnostics.Debug.WriteLine("Simulation Finished...");
                            CoordinatedShutdown.Get(Context.System).Run();
                        }
                    });

                    Receive<Schedule>(m =>
                    {
                        var sheduleAt = m.Delay + TimePeriod;
                        if (_FeaturedInstructions.TryGetValue(sheduleAt, out long value))
                            _FeaturedInstructions[sheduleAt] = _FeaturedInstructions[sheduleAt] + 1;
                        else
                            { _FeaturedInstructions.Add(sheduleAt, 1); }

                        m.Message.Target.Forward(m);
                    });

                    Receive<ISimulationMessage>(m =>
                    {
                        if (m.Target != null)
                            m.Target.Forward(m);
                        else if (m.TargetSelection != null)
                            m.TargetSelection.Tell(m);
                        else
                            Sender.Tell(m);

                        //System.Diagnostics.Debug.WriteLine("++" + m.GetType().ToString());
                        _CurrentInstructions++;
                    });

                    ReceiveAny(_ => Stash.Stash());
                });
            });

            ReceiveAny(_ => Stash.Stash());
        }


        private void Advance()
        {
            if (_IsRunning && !_IsComplete && _CurrentInstructions == 0)
            {
                if (_FeaturedInstructions.Count != 0)
                {
                    Advance(_FeaturedInstructions.Min(x => x.Key));
                } else {
                    _IsComplete = true;
                    Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, new Shutdown(Self), ActorRefs.NoSender);
                }
            }
        }

        private void Advance(long to)
        {
            if (_CurrentInstructions != 0) return;
            // advance time
            if (TimePeriod >= to)
                 throw new Exception("Time cant be undone.");
            TimePeriod = to;
            // get current Tasks
            MoveFeaturesToCurrentTimeSpan();
        }

        private void MoveFeaturesToCurrentTimeSpan()
        {
            if (_FeaturedInstructions.TryGetValue(TimePeriod, out _CurrentInstructions))
            {
                _FeaturedInstructions.Remove(TimePeriod);
                // global Tick
                var tick = new AdvanceTo(TimePeriod);
                Context.System.EventStream.Publish(tick);
                //System.Diagnostics.Debug.WriteLine("Move To: " + TimePeriod + " open " + _CurrentInstructions);
            }
        }
    }
}
