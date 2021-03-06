﻿using Akka.Actor;
using AkkaSim;
using SimTest.Domain;
using System;
using SimTest.MachineQueue;
using Akka.Event;

namespace SimTest.Machine
{
    partial class MachineAgent : SimulationElement
    {
        // Temp for test
        Random r = new Random(1337);

        public MachineAgent(IActorRef simulationContext, long time) : base(simulationContext, time)
        {
            Console.WriteLine("Time: " + TimePeriod + " - " + Self.Path + " is Ready");
        }
        public static Props Props(IActorRef simulationContext, long time)
        {
            return Akka.Actor.Props.Create(() => new MachineAgent(simulationContext, time));
        }

        protected override void Do(object o)
        {
            //switch (((ISimulationMessage)o).Message)
            switch (o)
            {
                case Work m: DoWork(m); break;
                case FinishWork f: WorkDone(f); break;
                default: new Exception("Message type could not be handled by SimulationElement"); break;
            }
        }

        private void DoWork(Work m)
        {
            var material = m.Message as MaterialRequest;
            var dur = material.Material.AssemblyDuration + r.Next(-1, 2);
            
            Schedule(dur, new FinishWork(m.Message, Self));
            //_SimulationContext.Tell(s, null);
            //Console.WriteLine("Time: " + TimePeriod + " - " + Self.Path + " - Working on: " + material.Material.Name);
        }

        private void WorkDone(FinishWork finishWork)
        {
            var material = finishWork.Message as MaterialRequest;
            _SimulationContext.Tell(new MachineJobDistributor.ProductionOrderFinished(material, Context.Parent), Self);
            //Console.WriteLine("Time: " + TimePeriod + " - " + Self.Path + " Finished: " + material.Material.Name);
        }

        protected override void Finish()
        {
            base.Finish();
        }


    }
}