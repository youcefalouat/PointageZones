

(function () { // Utilisation d'une IIFE (Immediately Invoked Function Expression) pour éviter de polluer la portée globale
    'use strict'; // Activation du mode strict

    // Vérifier si Dexie est chargé
    if (typeof Dexie === 'undefined') {
        console.error("La librairie Dexie.js n'est pas chargée. Veuillez vous assurer qu'elle est incluse avant db.js.");
        // Optionnel : Afficher une erreur à l'utilisateur ou arrêter l'exécution
        // document.body.innerHTML = '<div class="alert alert-danger">Erreur critique: La librairie de base de données locale (Dexie) est manquante.</div>';
        return; // Arrêter l'exécution de ce script
    }

    // --- Configuration ---
    const DATABASE_NAME = 'pointageAppDB'; // Choisissez un nom pour la base de données de votre application
    const DATABASE_VERSION = 6;           // Incrémentez ceci si vous changez le schéma ci-dessous

    // --- Définition de la Base de Données ---
    const db = new Dexie(DATABASE_NAME);

    // --- Définition du Schéma ---
    // Définir les tables (object stores) et leurs index
    // Pour Dexie v3+ (la plus récente au moment de l'écriture) :
    db.version(DATABASE_VERSION).stores({
        /**
         * toursData: Stocke les détails des tournées chargées en ligne par l'agent.
         * Utilisé pour afficher les informations de la tournée hors ligne et valider les scans.
         */
        toursData: [
            '&tourId',      // Clé primaire (ID unique de la tournée). '&' signifie index unique.
            'refTour',      // Index pour potentiellement rechercher/afficher la référence de la tournée.
            // 'zones' sera un tableau d'objets [{ pointageId, planTourId, zoneId, refZone }], non directement indexé ici.
        ].join(','), // Utiliser la séparation par virgule pour la syntaxe du schéma Dexie

        /**
         * pendingScans: Stocke les scans de code QR effectués hors ligne, en attente de synchro serveur.
         */
        pendingScans: [
            '&pointageId',      // Clé primaire (ID unique du scan). '&' signifie index unique.
            'planTourId',   // Index pour identifier l'assignation Tour/Zone spécifique scannée. Très important pour la synchro.
            'timestamp',    // Index pour potentiellement trier les scans par date.
            'qrCodeText', // Index composé pour vérifier rapidement si un scan PlanTour spécifique est déjà en attente.
            ].join(',')
    });

    // --- Optionnel : Ouvrir la base de données ---
    // Dexie ouvre la DB automatiquement lors de la première interaction.
    // Mais vous pouvez explicitement l'ouvrir pour gérer les erreurs potentielles tôt (ex: problèmes de mise à jour du schéma).
    db.open().then(() => {
        console.log(`IndexedDB "${DATABASE_NAME}" version ${DATABASE_VERSION} ouverte avec succès.`);
    }).catch(err => {
        console.error(`Échec de l'ouverture d'IndexedDB "${DATABASE_NAME}":`, err.stack || err);
        // Gérer les erreurs spécifiques comme Dexie.UpgradeError si nécessaire
        // Informer l'utilisateur que le stockage hors ligne pourrait ne pas fonctionner.
        alert(`Erreur critique lors de l'initialisation de la base de données locale (${err.name}). Le mode hors ligne risque de ne pas fonctionner.`);
    });


    // --- Rendre l'instance de base de données accessible ---
    // Option 1: Attacher à l'objet window (le plus simple pour les scripts non-modules)
    window.pointageDB = db;

    // Option 2: Si vous utilisez des modules ES6 (ex: avec Webpack, Rollup, ou <script type="module"> natif)
    // export default db;
    // Puis dans d'autres fichiers : import db from './db.js';

    console.log(`L'instance pointageDB est créée et potentiellement attachée à window ('window.pointageDB').`);

})(); // Fin de l'IIFE