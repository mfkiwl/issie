(*
    Simulator.fs

    This module collects all the APIs required for a simulation. 
*)

module Simulator

open DiagramTypes
open SimulatorTypes
open SynchronousUtils
open SimulationBuilder
open SimulationRunner
open DependencyMerger
open SimulationGraphAnalyser

// Simulating a circuit has four phases (not precisely in order of execution):
// 1. Building a simulation graph made of SimulationComponents.
// 2. Merging all the necessary dependencies.
// 3. Analyse the graph to look for errors, such as unconnected ports,
//    combinatorial loops, etc...
// 4. Setting the values of the input nodes of the graph to kickstart the
//    simulation process.

/// Builds the graph and simulates it with all inputs zeroed.
let prepareSimulation
        (diagramName : string)
        (canvasState : CanvasState)
        (loadedDependencies : LoadedComponent list)
        : Result<SimulationData, SimulationError> =
    match runCanvasStateChecksAndBuildGraph canvasState with
    | Error err -> Error err
    | Ok graph ->
        match mergeDependencies diagramName graph
                                canvasState loadedDependencies with
        | Error err -> Error err
        | Ok graph ->
            // Simulation graph is fully merged with dependencies.
            // Perform checks on it.
            let components, connections = canvasState
            let inputs, outputs = getSimulationIOs components
            match analyseSimulationGraph diagramName graph connections with
            | Some err -> Error err
            | None -> Ok {
                Graph = graph |> InitialiseGraphWithZeros inputs;
                Inputs = inputs;
                Outputs = outputs
                IsSynchronous = hasSynchronousComponents graph
            }

/// Expose the feedSimulationInput function from SimulationRunner.
let feedSimulationInput = SimulationRunner.feedSimulationInput

/// Expose the feedClockTick function from SimulationRunner.
let feedClockTick = SimulationRunner.feedClockTick

/// Expose the extractSimulationIOs function from SimulationRunner.
let extractSimulationIOs = SimulationRunner.extractSimulationIOs

/// Given a list of N generic elements, associate each element with a bit and
/// return 2^N lists with all the possible bit combinations.
/// A bit is simply a bus with width 1.
let makeAllBitCombinations (lst : 'a list) : (('a * WireData) list) list =
    let rec allCombinations lst result stack =
        match lst with
        | [] -> List.rev stack :: result
        | el :: lst' ->
            let result = allCombinations lst' result ((el,[Zero]) :: stack)
            allCombinations lst' result ((el,[One]) :: stack)
    List.rev <| allCombinations lst [] []
