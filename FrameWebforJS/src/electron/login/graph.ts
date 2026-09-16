import { Client } from '@microsoft/microsoft-graph-client'

export const getGraphClient = (accessToken) => {
    const graphClient = Client.init({
        authProvider: (done) => {
            done(null, accessToken);
        },
    });

    return graphClient;
};

